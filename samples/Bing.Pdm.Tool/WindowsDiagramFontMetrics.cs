using System.Runtime.InteropServices;
using Bing.Pdm;

namespace Bing.Pdm.Tool;

/// <summary>
/// 使用 Windows 字体测量图形文字。
/// </summary>
internal sealed class WindowsDiagramFontMetrics : IDisposable
{
    /// <summary>
    /// 保存离屏设备上下文。
    /// </summary>
    private readonly IntPtr _dc;
    /// <summary>
    /// 保存已创建字体。
    /// </summary>
    private readonly Dictionary<(string Family, int Size, bool Bold, bool Italic), IntPtr> _fonts = new();
    /// <summary>
    /// 保存是否已经释放。
    /// </summary>
    private bool _disposed;

    /// <summary>
    /// 初始化一个 <see cref="WindowsDiagramFontMetrics"/> 类型的实例。
    /// </summary>
    public WindowsDiagramFontMetrics()
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        _dc = CreateCompatibleDC(IntPtr.Zero);
        if (_dc == IntPtr.Zero) throw new IOException("Could not create a font measurement context.");
    }

    /// <summary>
    /// 测量指定样式的文字宽度。
    /// </summary>
    /// <remarks>字体高度直接使用图形逻辑单位，不依赖屏幕 DPI 或窗口缩放。</remarks>
    public double Measure(PdmDiagramTextMeasureInfo request)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (string.IsNullOrEmpty(request.Text)) return 0;
        var key = (request.FontFamily, request.FontSize, request.Bold, request.Italic);
        if (!_fonts.TryGetValue(key, out var font))
        {
            font = CreateFontW(-request.FontSize, 0, 0, 0, request.Bold ? 700 : 400,
                request.Italic ? 1u : 0u, 0, 0, 1, 0, 0, 5, 0, request.FontFamily);
            if (font == IntPtr.Zero) return double.NaN;
            _fonts.Add(key, font);
        }
        var old = SelectObject(_dc, font);
        if (old == IntPtr.Zero || old == new IntPtr(-1)) return double.NaN;
        try { return GetTextExtentPoint32W(_dc, request.Text, request.Text.Length, out var extent) ? extent.Width : double.NaN; }
        finally { SelectObject(_dc, old); }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var font in _fonts.Values) DeleteObject(font);
        DeleteDC(_dc);
    }

    /// <summary>
    /// Windows 文字尺寸。
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct TextExtent
    {
        /// <summary>
        /// 保存文字宽度。
        /// </summary>
        public int Width;
        /// <summary>
        /// 保存文字高度。
        /// </summary>
        public int Height;
    }

    /// <summary>
    /// 创建离屏设备上下文。
    /// </summary>
    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr dc);
    /// <summary>
    /// 创建设备字体。
    /// </summary>
    [DllImport("gdi32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern IntPtr CreateFontW(int height, int width, int escapement, int orientation,
        int weight, uint italic, uint underline, uint strikeOut, uint charSet, uint outputPrecision,
        uint clipPrecision, uint quality, uint pitchAndFamily, string family);
    /// <summary>
    /// 选择绘图对象。
    /// </summary>
    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr dc, IntPtr value);
    /// <summary>
    /// 读取当前字体的文字尺寸。
    /// </summary>
    [DllImport("gdi32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetTextExtentPoint32W(IntPtr dc, string text, int length, out TextExtent extent);
    /// <summary>
    /// 释放绘图对象。
    /// </summary>
    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr value);
    /// <summary>
    /// 释放设备上下文。
    /// </summary>
    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteDC(IntPtr dc);
}
