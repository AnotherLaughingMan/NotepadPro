using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Microsoft.Web.WebView2.Core;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace NotepadPro.Views;

/// <summary>
/// Avalonia <see cref="NativeControlHost"/> that embeds a Chromium-based WebView2 control.
/// The Monaco editor (TypeScript/HTML/CSS) runs inside this host and communicates with the
/// C# shell via a JSON message bridge.
/// </summary>
public sealed class WebViewHost : NativeControlHost
{
    // ── Win32 ────────────────────────────────────────────────────────────────

    private const uint WS_CHILD        = 0x40000000;
    private const uint WS_VISIBLE      = 0x10000000;
    private const uint WS_CLIPCHILDREN = 0x02000000;

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWindowEx(
        uint   dwExStyle,
        string lpClassName,
        string? lpWindowName,
        uint   dwStyle,
        int x, int y, int nWidth, int nHeight,
        IntPtr hWndParent,
        IntPtr hMenu,
        IntPtr hInstance,
        IntPtr lpParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyWindow(IntPtr hWnd);

    // ── State ────────────────────────────────────────────────────────────────

    private CoreWebView2Controller? _controller;

    /// <summary>Raised on the UI thread when the webview posts a JSON message.</summary>
    public event EventHandler<string>? MessageReceived;

    /// <summary>True once the WebView2 environment is fully initialised and navigated.</summary>
    public bool IsReady { get; private set; }

    // ── NativeControlHost overrides ──────────────────────────────────────────

    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        // Create a plain child HWND that WebView2 will use as its container.
        var hwnd = CreateWindowEx(
            0,
            "STATIC",
            null,
            WS_CHILD | WS_VISIBLE | WS_CLIPCHILDREN,
            0, 0, 100, 100,
            parent.Handle,
            IntPtr.Zero,
            IntPtr.Zero,
            IntPtr.Zero);

        if (hwnd == IntPtr.Zero)
            throw new InvalidOperationException($"CreateWindowEx failed: {Marshal.GetLastWin32Error()}");

        _ = InitAsync(hwnd);

        return new PlatformHandle(hwnd, "HWND");
    }

    protected override void DestroyNativeControlCore(IPlatformHandle control)
    {
        _controller?.Close();
        _controller = null;
        DestroyWindow(control.Handle);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var result = base.ArrangeOverride(finalSize);
        SyncControllerBounds(result);
        return result;
    }

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>Posts a JSON string to the webview's JavaScript context.</summary>
    public void PostMessage(string json)
    {
        if (_controller?.CoreWebView2 is { } wv)
            wv.PostWebMessageAsJson(json);
    }

    // ── Initialisation ───────────────────────────────────────────────────────

    private async Task InitAsync(IntPtr hwnd)
    {
        try
        {
            var cachePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "NotepadPro", "WebView2Cache");

            var env = await CoreWebView2Environment.CreateAsync(
                browserExecutableFolder: null,
                userDataFolder: cachePath);

            _controller = await env.CreateCoreWebView2ControllerAsync(hwnd);
            _controller.DefaultBackgroundColor = System.Drawing.Color.Transparent;

            var wv = _controller.CoreWebView2;

            wv.Settings.IsScriptEnabled         = true;
            wv.Settings.IsWebMessageEnabled      = true;
            wv.Settings.IsStatusBarEnabled       = false;
            wv.Settings.IsZoomControlEnabled     = false;
            wv.Settings.IsBuiltInErrorPageEnabled = false;
            wv.Settings.AreDefaultScriptDialogsEnabled = false;

#if DEBUG
            wv.Settings.AreDevToolsEnabled           = true;
            wv.Settings.AreDefaultContextMenusEnabled = true;
#else
            wv.Settings.AreDevToolsEnabled           = false;
            wv.Settings.AreDefaultContextMenusEnabled = false;
#endif

            // Map https://app.notepadpro/ → the wwwroot folder next to the executable.
            var wwwroot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
            wv.SetVirtualHostNameToFolderMapping(
                "app.notepadpro",
                wwwroot,
                CoreWebView2HostResourceAccessKind.Allow);

            wv.WebMessageReceived += (_, e) =>
                MessageReceived?.Invoke(this, e.TryGetWebMessageAsString());

            // Synchronise bounds one final time before navigating
            SyncControllerBounds(Bounds.Size);

            IsReady = true;
            wv.Navigate("https://app.notepadpro/index.html");
        }
        catch (Exception ex)
        {
            // Surface the error — most likely the WebView2 Runtime is not installed.
            Console.Error.WriteLine($"[WebViewHost] Initialisation failed: {ex}");
        }
    }

    private void SyncControllerBounds(Size size)
    {
        if (_controller is null || size.Width <= 0 || size.Height <= 0) return;

        var scale  = VisualRoot?.RenderScaling ?? 1.0;
        var width  = (int)(size.Width  * scale);
        var height = (int)(size.Height * scale);

        _controller.Bounds = new System.Drawing.Rectangle(0, 0, width, height);
    }
}
