using System.Runtime.Versioning;

namespace AdaptNess;

internal sealed record AdaptiveBrightnessSettings(
    TimeSpan AnalysisInterval = default, TimeSpan SmoothingTime = default,
    TimeSpan MinimumUpdateInterval = default, double Deadband = 4,
    int MaximumStep = 4, int Minimum = 28, int Maximum = 50)
{
    public TimeSpan EffectiveAnalysisInterval => AnalysisInterval == default ? TimeSpan.FromMilliseconds(250) : AnalysisInterval;
    public TimeSpan EffectiveSmoothingTime => SmoothingTime == default ? TimeSpan.FromSeconds(0.7) : SmoothingTime;
    public TimeSpan EffectiveMinimumUpdateInterval => MinimumUpdateInterval == default ? TimeSpan.FromMilliseconds(650) : MinimumUpdateInterval;
}

[SupportedOSPlatform("windows")]
internal sealed class AdaptiveBrightnessEngine
{
    private readonly BrightnessController brightness;
    private readonly ScreenCapture capture;
    private readonly AdaptiveBrightnessSettings settings;

    public AdaptiveBrightnessEngine(BrightnessController brightness, ScreenCapture capture, AdaptiveBrightnessSettings settings)
    { this.brightness = brightness; this.capture = capture; this.settings = settings; }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        if (!brightness.TryGetCurrentBrightness(out var current))
        { AppLog.Info("Could not read display brightness; adaptive mode is disabled."); return; }
        AppLog.Info($"Current brightness: {current}%.");
        // The display's starting brightness is the current physical state, not
        // an automatic-target offset. Only a verified manual change supplies
        // a user preference offset.
        var manualOffset = 0;
        var controlState = new AdaptiveControlState(current, settings.Deadband);
        var smoothed = double.NaN;
        var lastUpdate = DateTimeOffset.UtcNow;
        var lastExternalCheck = DateTimeOffset.MinValue;
        var lastLog = DateTimeOffset.MinValue;
        var captureFailureReported = false;
        while (!cancellationToken.IsCancellationRequested)
        {
            var started = DateTimeOffset.UtcNow;
            var pixels = capture.CaptureBgra();
            if (pixels is null)
            {
                if (!captureFailureReported) { AppLog.Info("Screen capture failed; holding brightness."); captureFailureReported = true; }
            }
            else
            {
                captureFailureReported = false;
                var luminance = LuminanceAnalyzer.CalculateNormalized(pixels);
                var alpha = 1 - Math.Exp(-settings.EffectiveAnalysisInterval.TotalSeconds / settings.EffectiveSmoothingTime.TotalSeconds);
                smoothed = double.IsNaN(smoothed) ? luminance : smoothed + alpha * (luminance - smoothed);
                var mapped = MapLuminanceToBrightness(smoothed, settings.Minimum, settings.Maximum);
                var rawTarget = Math.Clamp(mapped + manualOffset, settings.Minimum, settings.Maximum);
                var previousTarget = controlState.StableTarget;
                var target = controlState.UpdateTarget(rawTarget);
                if (target != previousTarget)
                    AppLog.Info($"Target accepted: mapped {mapped}%, stable {target}%.");
                if (DateTimeOffset.UtcNow - lastLog >= TimeSpan.FromSeconds(5))
                {
                    AppLog.Info($"Content luminance: {luminance:P0}; EMA: {smoothed:P0}; mapped: {mapped}%; stable target: {target}%; requested: {controlState.LastRequested}%.");
                    lastLog = DateTimeOffset.UtcNow;
                }
                if (DateTimeOffset.UtcNow - lastExternalCheck >= TimeSpan.FromSeconds(5) && brightness.TryGetCurrentBrightness(out var observed))
                {
                    lastExternalCheck = DateTimeOffset.UtcNow;
                    if (controlState.TryDetectManualChange(observed, DateTimeOffset.UtcNow, out var manualBrightness))
                    {
                        manualOffset = Math.Clamp(manualBrightness - mapped, settings.Minimum - settings.Maximum, settings.Maximum - settings.Minimum);
                        controlState.RecordManualBrightness(manualBrightness);
                        AppLog.Info($"Manual brightness change detected; using {manualBrightness}% as the new preference.");
                    }
                    else
                    {
                        AppLog.Info($"Brightness readback: observed {observed}%, expected {controlState.LastRequested}%; classified as automatic/tolerated.");
                    }
                }
                if (target != controlState.LastRequested && DateTimeOffset.UtcNow - lastUpdate >= settings.EffectiveMinimumUpdateInterval)
                {
                    var next = controlState.GetNextBrightness(settings.MaximumStep);
                    if (brightness.SetBrightness(next)) { controlState.RecordAutomaticRequest(next, DateTimeOffset.UtcNow); lastUpdate = DateTimeOffset.UtcNow; }
                    if (brightness.IsUnavailable) return;
                }
            }
            var delay = settings.EffectiveAnalysisInterval - (DateTimeOffset.UtcNow - started);
            if (delay > TimeSpan.Zero)
                try { await Task.Delay(delay, cancellationToken); } catch (OperationCanceledException) { }
        }
    }

    public static int MapLuminanceToBrightness(double luminance, int minimum = 28, int maximum = 50)
    {
        var normalized = Math.Clamp(luminance, 0, 1);
        var smooth = normalized * normalized * (3 - 2 * normalized);
        // Invert a full-range smoothstep curve while keeping a small margin at
        // the bright end. This gives dark content about 50% and white content
        // about 28%, without making low luminance jump to the old 85% target.
        var inverted = 0.02 + 0.98 * (1 - smooth);
        return (int)Math.Round(minimum + (maximum - minimum) * inverted);
    }
}

internal sealed class AdaptiveControlState
{
    // Manual brightness is polled every five seconds. Keep an automatic
    // request pending through at least one poll so a delayed WMI readback
    // cannot become a false manual change.
    private static readonly TimeSpan AutomaticReadbackGrace = TimeSpan.FromSeconds(8);
    private readonly double deadband;
    private int stableTarget;
    private int? pendingAutomaticRequest;
    private DateTimeOffset lastAutomaticRequestAt = DateTimeOffset.MinValue;
    private int? lastDetectedManualBrightness;

    public AdaptiveControlState(int initialBrightness, double deadband)
    {
        LastRequested = initialBrightness;
        stableTarget = initialBrightness;
        this.deadband = deadband;
    }

    public int LastRequested { get; private set; }
    public int StableTarget => stableTarget;

    public int UpdateTarget(int rawTarget)
    {
        // The EMA may produce a naturally changing integer target on every
        // sample. Accept a meaningful move immediately, but hold changes
        // inside the deadband. Strict comparison prevents a +/-4 point edge
        // from repeatedly flipping the stable target.
        if (Math.Abs(rawTarget - stableTarget) > deadband)
            stableTarget = rawTarget;
        return stableTarget;
    }

    public int GetNextBrightness(int maximumStep)
    {
        return LastRequested + Math.Clamp(stableTarget - LastRequested, -maximumStep, maximumStep);
    }

    public void RecordAutomaticRequest(int requestedBrightness, DateTimeOffset timestamp)
    {
        LastRequested = requestedBrightness;
        pendingAutomaticRequest = requestedBrightness;
        lastAutomaticRequestAt = timestamp;
    }

    public void RecordManualBrightness(int observedBrightness)
    {
        LastRequested = observedBrightness;
        pendingAutomaticRequest = null;
    }

    public bool TryDetectManualChange(int observedBrightness, DateTimeOffset timestamp, out int manualBrightness)
    {
        manualBrightness = observedBrightness;

        // A readback at or within one point of our request is normal WMI
        // behavior, including integer rounding, and is never manual input.
        if (Math.Abs(observedBrightness - LastRequested) <= 1)
        {
            pendingAutomaticRequest = null;
            return false;
        }

        // Ignore a temporarily stale readback while a recent automatic request
        // is still settling. This prevents our own write from becoming a
        // baseline change on the next WMI poll.
        if (pendingAutomaticRequest.HasValue && timestamp - lastAutomaticRequestAt < AutomaticReadbackGrace)
            return false;

        if (Math.Abs(observedBrightness - LastRequested) < 3 || lastDetectedManualBrightness == observedBrightness)
            return false;

        lastDetectedManualBrightness = observedBrightness;
        pendingAutomaticRequest = null;
        return true;
    }
}

internal static class SelfTests
{
    public static void Run()
    {
        Assert(Math.Abs(LuminanceAnalyzer.CalculateNormalized(new byte[] { 0, 0, 255, 0 }) - 0.2126) < 0.001, "red luminance");
        Assert(LuminanceAnalyzer.CalculateNormalized(new byte[] { 255, 255, 255, 0 }) > 0.99, "white luminance");
        Assert(AdaptiveBrightnessEngine.MapLuminanceToBrightness(0) == 50, "dark target");
        Assert(AdaptiveBrightnessEngine.MapLuminanceToBrightness(0.4) == 42, "medium target");
        Assert(AdaptiveBrightnessEngine.MapLuminanceToBrightness(0.5) == 39, "mid-gray target");
        Assert(AdaptiveBrightnessEngine.MapLuminanceToBrightness(0.6) == 36, "further medium target");
        Assert(AdaptiveBrightnessEngine.MapLuminanceToBrightness(0.8) is >= 30 and <= 31, "bright target");
        Assert(AdaptiveBrightnessEngine.MapLuminanceToBrightness(1) == 28, "bright target");

        var initial = new AdaptiveControlState(64, 4);
        initial.UpdateTarget(50);
        Assert(NextAndRecord(initial, 4, 60) && NextAndRecord(initial, 4, 56) && NextAndRecord(initial, 4, 52) && NextAndRecord(initial, 4, 50), "initial convergence");

        var transitions = new AdaptiveControlState(50, 4);
        Assert(transitions.UpdateTarget(42) == 42, "dark to medium target accepted");
        Assert(transitions.UpdateTarget(36) == 36, "medium to brighter target accepted");
        Assert(transitions.UpdateTarget(31) == 31, "bright target accepted");

        var automatic = new AdaptiveControlState(54, 4);
        automatic.RecordAutomaticRequest(50, DateTimeOffset.UnixEpoch);
        Assert(!automatic.TryDetectManualChange(54, DateTimeOffset.UnixEpoch.AddSeconds(5), out _), "stale automatic readback ignored");
        Assert(!automatic.TryDetectManualChange(50, DateTimeOffset.UnixEpoch.AddSeconds(5), out _), "automatic change ignored");
        Assert(!automatic.TryDetectManualChange(49, DateTimeOffset.UnixEpoch.AddSeconds(6), out _), "WMI rounding ignored");

        var manual = new AdaptiveControlState(54, 4);
        manual.RecordAutomaticRequest(50, DateTimeOffset.UnixEpoch);
        Assert(manual.TryDetectManualChange(60, DateTimeOffset.UnixEpoch.AddSeconds(9), out _), "manual change detected");
        Assert(!manual.TryDetectManualChange(60, DateTimeOffset.UnixEpoch.AddSeconds(14), out _), "manual baseline not repeated");

        var stable = new AdaptiveControlState(50, 4);
        stable.UpdateTarget(45);
        var writes = 0;
        while (stable.LastRequested != 45)
        {
            var next = stable.GetNextBrightness(4);
            Assert(next < stable.LastRequested, "stable target moves downward");
            Assert(next >= stable.StableTarget, "stable target never overshot");
            stable.RecordAutomaticRequest(next, DateTimeOffset.UnixEpoch.AddSeconds(++writes));
        }
        Assert(writes == 2 && stable.LastRequested == 45, "stable target converges without oscillation");

        var noise = new AdaptiveControlState(42, 4);
        foreach (var rawTarget in new[] { 40, 44, 39, 45, 38, 46 })
            Assert(noise.UpdateTarget(rawTarget) == 42, "deadband rejects small target noise");

        var changing = new AdaptiveControlState(50, 4);
        changing.UpdateTarget(36);
        changing.RecordAutomaticRequest(46, DateTimeOffset.UnixEpoch);
        changing.UpdateTarget(48);
        Assert(changing.GetNextBrightness(4) == 48, "new target retargets while converging");

        var oneWrite = new AdaptiveControlState(50, 4);
        oneWrite.UpdateTarget(42);
        var planned = oneWrite.GetNextBrightness(4);
        Assert(planned == 46 && oneWrite.LastRequested == 50, "one iteration plans one write");
        oneWrite.RecordAutomaticRequest(planned, DateTimeOffset.UnixEpoch);
        Assert(oneWrite.LastRequested == 46, "one write commits one requested value");
        AppLog.Info("Self-tests passed.");
    }

    private static bool NextAndRecord(AdaptiveControlState state, int maximumStep, int expected)
    {
        var next = state.GetNextBrightness(maximumStep);
        state.RecordAutomaticRequest(next, DateTimeOffset.UnixEpoch);
        return next == expected;
    }
    private static void Assert(bool condition, string name)
    { if (!condition) throw new InvalidOperationException($"Self-test failed: {name}"); }
}
