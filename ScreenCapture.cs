using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace AdaptNess;

[SupportedOSPlatform("windows")]
internal sealed class ScreenCapture : IDisposable
{
    private const uint Srccopy = 0x00CC0020;
    private const uint CaptureBlt = 0x40000000;
    private readonly int width;
    private readonly int height;
    private bool disposed;

    public ScreenCapture(int width, int height) { this.width = width; this.height = height; }

    public byte[]? CaptureBgra()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var sourceWidth = GetSystemMetrics(0);
        var sourceHeight = GetSystemMetrics(1);
        var screen = GetDC(IntPtr.Zero);
        var memory = IntPtr.Zero;
        var bitmap = IntPtr.Zero;
        var previous = IntPtr.Zero;
        try
        {
            if (screen == IntPtr.Zero || sourceWidth <= 0 || sourceHeight <= 0) return null;
            memory = CreateCompatibleDC(screen);
            bitmap = CreateCompatibleBitmap(screen, width, height);
            if (memory == IntPtr.Zero || bitmap == IntPtr.Zero) return null;
            previous = SelectObject(memory, bitmap);
            if (!StretchBlt(memory, 0, 0, width, height, screen, 0, 0, sourceWidth, sourceHeight, Srccopy | CaptureBlt)) return null;
            var info = new BitmapInfo { Header = new BitmapInfoHeader {
                Size = (uint)Marshal.SizeOf<BitmapInfoHeader>(), Width = width, Height = -height,
                Planes = 1, BitCount = 32, Compression = 0 } };
            var pixels = new byte[width * height * 4];
            return GetDIBits(memory, bitmap, 0, (uint)height, pixels, ref info, 0) == height ? pixels : null;
        }
        catch (Win32Exception) { return null; }
        finally
        {
            if (previous != IntPtr.Zero) SelectObject(memory, previous);
            if (bitmap != IntPtr.Zero) DeleteObject(bitmap);
            if (memory != IntPtr.Zero) DeleteDC(memory);
            if (screen != IntPtr.Zero) ReleaseDC(IntPtr.Zero, screen);
        }
    }

    public void Dispose() => disposed = true;

    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr window);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr window, IntPtr dc);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleBitmap(IntPtr dc, int width, int height);
    [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr dc, IntPtr objectHandle);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr objectHandle);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr dc);
    [DllImport("gdi32.dll")] private static extern bool StretchBlt(IntPtr destination, int x, int y, int width, int height, IntPtr source, int sourceX, int sourceY, int sourceWidth, int sourceHeight, uint operation);
    [DllImport("gdi32.dll")] private static extern int GetDIBits(IntPtr dc, IntPtr bitmap, uint start, uint lines, byte[] bits, ref BitmapInfo info, uint usage);

    [StructLayout(LayoutKind.Sequential)] private struct BitmapInfo { public BitmapInfoHeader Header; }
    [StructLayout(LayoutKind.Sequential)] private struct BitmapInfoHeader
    {
        public uint Size; public int Width; public int Height; public ushort Planes; public ushort BitCount;
        public uint Compression; public uint ImageSize; public int XPelsPerMeter; public int YPelsPerMeter;
        public uint ColorsUsed; public uint ColorsImportant;
    }
}
