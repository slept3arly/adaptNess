namespace AdaptNess;

internal static class LuminanceAnalyzer
{
    public static double CalculateNormalized(byte[] bgra)
    {
        if (bgra.Length == 0 || bgra.Length % 4 != 0) throw new ArgumentException("Expected BGRA pixels.", nameof(bgra));
        double total = 0;
        for (var i = 0; i < bgra.Length; i += 4)
            total += (0.0722 * bgra[i] + 0.7152 * bgra[i + 1] + 0.2126 * bgra[i + 2]) / 255.0;
        return total / (bgra.Length / 4);
    }
}
