namespace AdaptNess;

internal sealed class AdaptiveEngineHost : IAsyncDisposable
{
    private readonly object gate = new();
    private CancellationTokenSource? cancellation;
    private Task? engineTask;

    public bool IsRunning
    {
        get { lock (gate) return engineTask is { IsCompleted: false }; }
    }

    public void Start(AppSettings settings)
    {
        lock (gate)
        {
            if (engineTask is { IsCompleted: false }) return;
            cancellation?.Dispose();
            cancellation = new CancellationTokenSource();
            var token = cancellation.Token;
            var normalized = settings.Normalize();
            engineTask = Task.Run(() => RunAsync(normalized, token), token);
        }
    }

    public async Task StopAsync()
    {
        CancellationTokenSource? source;
        Task? task;
        lock (gate)
        {
            source = cancellation;
            task = engineTask;
            source?.Cancel();
        }
        if (task is null) return;
        try { await task.ConfigureAwait(true); }
        catch (OperationCanceledException) { }
        finally
        {
            lock (gate)
            {
                if (task == engineTask)
                {
                    cancellation?.Dispose();
                    cancellation = null;
                    engineTask = null;
                }
            }
        }
    }

    private static async Task RunAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        try
        {
            using var brightness = new BrightnessController(new BrightnessSettings(settings.MinimumBrightness, settings.MaximumBrightness));
            using var capture = new ScreenCapture(32, 18);
            var engine = new AdaptiveBrightnessEngine(brightness, capture,
                new AdaptiveBrightnessSettings(Minimum: settings.MinimumBrightness, Maximum: settings.MaximumBrightness));
            await engine.RunAsync(cancellationToken);
            if (!cancellationToken.IsCancellationRequested)
                AppLog.Info("Adaptive engine stopped unexpectedly; no further automatic changes will be made.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception exception)
        {
            AppLog.Error("Adaptive engine stopped after an unexpected error.", exception);
        }
    }

    public async ValueTask DisposeAsync() => await StopAsync();
}
