using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using QuickText.App.Interop;

namespace QuickText.App.Ui;

internal static class WindowTheming
{
    /// <summary>Mirror the window for a right-to-left UI language (Arabic). WPF flips standard
    /// layout automatically; call once at construction, so a language change takes effect on
    /// windows opened afterwards.</summary>
    public static void ApplyFlowDirection(Window w) =>
        w.FlowDirection = Core.Localization.LocalizationService.Instance.Culture.TextInfo.IsRightToLeft
            ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

    /// <summary>Match the native title bar to the app's own theme. Named for what it did when there
    /// was only one theme; it now follows <see cref="ThemeService.IsLight"/>, because a dark title
    /// bar over light content looks like a half-repainted window.</summary>
    public static void UseDarkChrome(Window w)
    {
        w.SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(w).Handle;
            int on = ThemeService.IsLight ? 0 : 1;
            try
            {
                NativeMethods.DwmSetWindowAttribute(
                    hwnd, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE, ref on, sizeof(int));
            }
            catch { /* older OS without the attribute — harmless */ }
            // Cloak until WPF has presented its first frame. Between ShowWindow and that first
            // present there is a real gap. Measured by sampling screen pixels while a window opened:
            // the window's first DWM-composed frame arrives ~50ms BEFORE the content and is plain
            // white, i.e. a white flash on every dark-theme open. A WM_ERASEBKGND hook was tried
            // first and disproven — the white is the surface's initial composition, before any
            // erase cycle.
            // Cloaking (the flag DWM itself uses for windows on inactive virtual desktops) keeps
            // the window out of composition until the content frame exists, and costs no perceived
            // latency: the measured white phase is replaced by the same duration of nothing.
            Cloak(hwnd, true);
            // Backstop. ContentRendered below is the normal reveal, but WPF does not promise it: it
            // never fires for a window with no content, and it is a low-priority dispatcher callback
            // that a window closed or hidden before its first render can miss. With only that one
            // path, a miss leaves a window that is invisible on screen yet holds focus and sits in
            // alt-tab — and for the ShowDialog() windows (AppDialog, VariablesDialog, BodyEditor)
            // that is an invisible MODAL: the owner stops responding and there is nothing to click.
            // A flash is a blemish; an unclosable window is a lost session, so the reveal gets a
            // timer that fires regardless. Uncloaking twice is a no-op.
            var reveal = new System.Windows.Threading.DispatcherTimer(
                System.Windows.Threading.DispatcherPriority.Loaded, w.Dispatcher)
                { Interval = TimeSpan.FromMilliseconds(600) };
            reveal.Tick += (_, _) => { reveal.Stop(); Cloak(new WindowInteropHelper(w).Handle, false); };
            reveal.Start();
        };
        // First frame is committed; let DWM show the window.
        w.ContentRendered += (_, _) => Cloak(new WindowInteropHelper(w).Handle, false);
    }

    /// <summary>Hide or reveal a window at the DWM level. Note this cannot report failure: the API
    /// returns an HRESULT rather than throwing, so a refused uncloak is invisible here — which is
    /// exactly why the reveal has two independent triggers rather than one.</summary>
    private static void Cloak(IntPtr hwnd, bool on)
    {
        if (hwnd == IntPtr.Zero) return;
        int v = on ? 1 : 0;
        try { NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_CLOAK, ref v, sizeof(int)); }
        catch { /* pre-Win8 dwmapi without the attribute — falls back to the flash, not a failure */ }
    }

    /// <summary>Breathing room left below a capped dialog so it does not sit flush against the
    /// taskbar edge.</summary>
    private const double HeightCapMargin = 72;

    /// <summary>Cap a <c>SizeToContent="Height"</c> dialog to the work area of the monitor it opens
    /// on, WITHOUT touching where it opens (unlike <see cref="PlaceOnActiveMonitor"/>, which also
    /// repositions — wrong for a CenterOwner modal).
    /// <para>A window whose height is decided entirely by its content has no upper bound. Enough
    /// <c>{variables}</c> in one snippet, or a long enough message, and it grows past the bottom of
    /// the screen — and because these dialogs are <c>ResizeMode="NoResize"</c>, what goes off the
    /// bottom is the OK button, with no way to resize or scroll down to it. The user cannot finish
    /// the action at all, only Esc out of it.</para>
    /// <para>The cap cannot be a constant: the same number has to serve a 1366x768 laptop (about
    /// 614 DIP of work area) and a 4K display, and it is wrong at one end or the other.</para>
    /// <para>Measured against the OWNER's monitor rather than this window's. At SourceInitialized a
    /// CenterOwner / CenterScreen dialog has not been positioned yet, so asking which monitor IT is
    /// on describes where it started, not where it will land.</para>
    /// <para>Pair this with a ScrollViewer around the part that grows. Capping alone only moves the
    /// clip — the buttons still disappear, just at a different height.</para></summary>
    public static void CapHeightToMonitor(Window w)
    {
        w.SourceInitialized += (_, _) =>
        {
            if (App.InSmoke) return;   // --shots sets its own simulated work-area cap after Show
            double cap = OwnerOrCursorWorkArea(w).Height - HeightCapMargin;
            w.MaxHeight = Math.Max(cap, w.MinHeight);
        };
    }

    /// <summary>Horizontally centre on the owner (or cursor) monitor, with Top = work-area top +
    /// <paramref name="topOffset"/>. For dialogs that should sit near the top rather than
    /// <c>CenterOwner</c> (e.g. AI generate, which grows downward as the result appears).</summary>
    public static void PlaceNearTop(Window w, double topOffset = 50)
    {
        w.WindowStartupLocation = WindowStartupLocation.Manual;
        Rect wa = default;
        bool settled = false;
        void Place()
        {
            if (settled || App.InSmoke) return;
            if (w.WindowState == WindowState.Maximized) return;
            double width = w.ActualWidth > 0 ? w.ActualWidth : w.Width;
            double height = w.ActualHeight;
            double left = wa.Left + Math.Max(0, (wa.Width - width) / 2);
            double top = wa.Top + topOffset;
            if (height > 0 && top + height > wa.Bottom) top = Math.Max(wa.Top, wa.Bottom - height);
            w.Left = Math.Max(wa.Left, Math.Min(left, wa.Right - width));
            w.Top = top;
        }
        w.SourceInitialized += (_, _) =>
        {
            wa = OwnerOrCursorWorkAreaForPlacement(w);
            Place();
        };
        w.Loaded += (_, _) => Place();
        w.ContentRendered += (_, _) => { Place(); settled = true; };
    }

    /// <summary>Owner/cursor work area in <paramref name="w"/>'s DIP space for Left/Top writes.</summary>
    private static Rect OwnerOrCursorWorkAreaForPlacement(Window w)
    {
        if (w.Owner is { } owner)
        {
            var oh = new WindowInteropHelper(owner).Handle;
            if (oh != IntPtr.Zero)
                return MonitorWorkAreaFor(w,
                    NativeMethods.MonitorFromWindow(oh, NativeMethods.MONITOR_DEFAULTTONEAREST));
        }
        return CursorWorkArea(w);
    }

    /// <summary>Work area (DIPs) of the monitor the owner window is on; the cursor's monitor when
    /// there is no owner (VariablesDialog is summoned from the panel and can be ownerless, and the
    /// parameterless BodyEditorWindow ctor used by --smoke has none either).
    /// <para>"Where will this child window land?" — the question every owned window has to answer
    /// before it can size or cap itself, and it cannot ask its OWN handle: at SourceInitialized a
    /// CenterOwner / CenterScreen window has not been positioned yet.</para></summary>
    internal static Rect OwnerOrCursorWorkArea(Window w)
    {
        // Destination-monitor DIP (not TransformFromDevice): callers use .Height for MaxHeight
        // caps that must remain correct after WM_DPICHANGED on the landing monitor.
        if (w.Owner is { } owner)
        {
            var oh = new WindowInteropHelper(owner).Handle;
            if (oh != IntPtr.Zero)
                return MonitorWorkAreaDip(
                    NativeMethods.MonitorFromWindow(oh, NativeMethods.MONITOR_DEFAULTTONEAREST));
        }
        return CursorWorkArea();
    }

    /// <summary>Maximize on the cursor's monitor without the top-offset used by
    /// <see cref="PlaceOnActiveMonitor"/> (that offset leaves a gap under a maximized window).</summary>
    public static void MaximizeOnActiveMonitor(Window w)
    {
        w.WindowStartupLocation = WindowStartupLocation.Manual;
        // Loaded (not SourceInitialized): --smoke parks with ShowActivated=false, which WPF forbids
        // combining with Maximized at Show time. Loaded runs after Show has begun safely.
        w.Loaded += (_, _) =>
        {
            if (App.InSmoke || !w.ShowActivated) return;
            if (w.WindowState == WindowState.Maximized) return;
            var wa = CursorWorkArea(w);
            // Park on the target monitor first — Maximized alone can stick to the primary display.
            w.WindowState = WindowState.Normal;
            w.MaxHeight = double.PositiveInfinity;
            w.MaxWidth = double.PositiveInfinity;
            w.Left = wa.Left;
            w.Top = wa.Top;
            w.Width = Math.Max(w.MinWidth, wa.Width);
            w.Height = Math.Max(w.MinHeight, wa.Height);
            w.WindowState = WindowState.Maximized;
        };
    }

    /// <summary>
    /// Place a dialog (Settings / Manager) the same way the search panel positions itself: on the
    /// monitor the user is actually on — the one under the cursor, where they clicked to open it —
    /// centered, capped to that monitor's work area, and nudged fully on-screen so nothing (the
    /// Save button, the editor) ends up under the taskbar or off a short / secondary display.
    /// One shared implementation so each window doesn't reinvent (or forget) it.
    /// <para>The work-area <see cref="Window.MaxHeight"/> is only a Normal-state resize cap. While
    /// Maximized it must be lifted: a secondary with no taskbar has work area ≈ full height, and
    /// WPF's maximize frame then sits MaxHeight below a slightly negative Top — the bottom chrome
    /// sticks out past the monitor. Primary often hides the same bug because its taskbar already
    /// shortens the work area. Restore re-applies the cap for the monitor the window is on.</para>
    /// </summary>
    public static void PlaceOnActiveMonitor(Window w)
    {
        w.WindowStartupLocation = WindowStartupLocation.Manual;
        Rect wa = default;
        bool settled = false;
        void Place()
        {
            if (settled || App.InSmoke) return;   // --smoke parks windows off-screen and never shows them
            // Never write Left/Top under Maximized — that fights the system maximize frame.
            if (w.WindowState == WindowState.Maximized) return;
            // Top-centered: horizontal centre needs only the width (a set Width, or ActualWidth once
            // laid out), and the vertical anchor is a fixed offset — so this can run in
            // SourceInitialized, BEFORE the first paint and before SizeToContent finalizes. Centering
            // vertically would need the height, which isn't known that early, forcing a post-paint
            // reposition that flashes. If the (auto-)height then overflows the work area, pull up.
            double width = w.ActualWidth > 0 ? w.ActualWidth : w.Width;
            double height = w.ActualHeight;
            double left = wa.Left + Math.Max(0, (wa.Width - width) / 2);
            double top = wa.Top + wa.Height * 0.08;
            if (height > 0 && top + height > wa.Bottom) top = Math.Max(wa.Top, wa.Bottom - height);
            w.Left = Math.Max(wa.Left, Math.Min(left, wa.Right - width));
            w.Top = top;
        }
        w.SourceInitialized += (_, _) =>
        {
            // Position in THIS window's current DIP space (TransformFromDevice). MaxHeight uses the
            // destination monitor's own DIP height so the cap stays correct after WM_DPICHANGED.
            var mon = CursorMonitor();
            wa = MonitorWorkAreaFor(w, mon);
            w.MaxHeight = MonitorWorkAreaDip(mon).Height;
            Place();   // position pre-paint so there's no origin→final flash
        };
        // Re-assert once the real size is known (pulls a very tall window up to fit); stop after the
        // window is shown so a later user resize (Manager is resizable) isn't yanked back.
        w.Loaded += (_, _) => Place();
        w.SizeChanged += (_, _) => Place();
        w.ContentRendered += (_, _) => { Place(); settled = true; };
        w.StateChanged += (_, _) =>
        {
            if (App.InSmoke) return;
            if (w.WindowState == WindowState.Maximized)
            {
                // Same lift MaximizeOnActiveMonitor does before maximizing.
                w.MaxHeight = double.PositiveInfinity;
                return;
            }
            if (w.WindowState != WindowState.Normal) return;
            var hwnd = new WindowInteropHelper(w).Handle;
            var mon = hwnd != IntPtr.Zero
                ? NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MONITOR_DEFAULTTONEAREST)
                : CursorMonitor();
            w.MaxHeight = MonitorWorkAreaDip(mon).Height;
        };
    }

    /// <summary>Per-MONITOR device-px → DIP factor (96 / that monitor's effective DPI), via
    /// Shcore's <c>GetDpiForMonitor</c>. The app is PerMonitorV2-DPI-aware (see app.manifest), so
    /// there is no longer a single scale for the whole virtual desktop — every monitor-geometry
    /// conversion must ask for ITS OWN monitor's DPI. Falls back to 1.0 (no scaling) if the handle
    /// is null or the API fails — GetDpiForMonitor has existed since Windows 8.1, well below this
    /// app's minimum OS, so that path is not expected to be hit in practice.</summary>
    internal static double PxToDipFactor(IntPtr monitor)
    {
        try
        {
            if (monitor != IntPtr.Zero &&
                NativeMethods.GetDpiForMonitor(monitor, NativeMethods.MDT_EFFECTIVE_DPI, out uint dpiX, out _) == 0 &&
                dpiX > 0)
                return 96.0 / dpiX;
        }
        catch { /* Shcore unavailable — fall through */ }
        return 1.0;
    }

    /// <summary>Work area (DIPs) of the given monitor, converted with THAT monitor's own DPI; the
    /// primary work area if the handle is null or unavailable.
    /// <para>Use for <b>sizes</b> (MaxHeight caps) that must match the destination monitor after
    /// <c>WM_DPICHANGED</c>. For <see cref="Window.Left"/>/<see cref="Window.Top"/>, prefer
    /// <see cref="MonitorWorkAreaFor"/> — mixed-DPI desktops need the window's current
    /// <c>TransformFromDevice</c>, not the destination monitor's scale alone.</para></summary>
    internal static Rect MonitorWorkAreaDip(IntPtr monitor)
    {
        if (TryMonitorWorkAreaPx(monitor, out var rc))
        {
            double k = PxToDipFactor(monitor);
            return new Rect(rc.Left * k, rc.Top * k,
                (rc.Right - rc.Left) * k, (rc.Bottom - rc.Top) * k);
        }
        return SystemParameters.WorkArea;
    }

    /// <summary>
    /// Work area in the DIP space of <paramref name="w"/>'s current composition transform — safe to
    /// assign to <see cref="Window.Left"/>/<see cref="Window.Top"/> under PerMonitorV2.
    /// <para>Measured: with primary @200% and secondary @100%, converting secondary <c>rcWork</c>
    /// by secondary DPI alone and writing that into Left while the window still carried the
    /// primary scale landed the panel at ~2× physical X (past the desktop edge) — hotkey on the
    /// secondary looked like a no-op. <c>TransformFromDevice</c> of the window being moved is the
    /// same path floating-ball drag already uses.</para>
    /// </summary>
    internal static Rect MonitorWorkAreaFor(Window w, IntPtr monitor)
    {
        if (TryMonitorWorkAreaPx(monitor, out var rc) && TryTransformFromDevice(w, out var tfd))
        {
            var tl = tfd.Transform(new Point(rc.Left, rc.Top));
            var br = tfd.Transform(new Point(rc.Right, rc.Bottom));
            return new Rect(tl, br);
        }
        return MonitorWorkAreaDip(monitor);
    }

    /// <summary>Device px → DIP via <paramref name="w"/>'s composition transform; false when the
    /// window has no source yet and <c>EnsureHandle</c> cannot create one.</summary>
    internal static bool TryDeviceToDip(Window w, double xPx, double yPx, out Point dip)
    {
        dip = default;
        if (!TryTransformFromDevice(w, out var tfd)) return false;
        dip = tfd.Transform(new Point(xPx, yPx));
        return true;
    }

    /// <summary>
    /// Work area (DIPs) of the monitor containing the given DIP point; the primary's as a fallback.
    /// <para>This is what a REMEMBERED window position must be clamped against.
    /// <see cref="SystemParameters.WorkArea"/> describes the PRIMARY monitor only, so clamping a
    /// remembered position to it drags any window that was left on another monitor back onto the
    /// primary — and for a monitor sitting to the LEFT of the primary (negative X, which Windows
    /// allows) the clamp pins it to x=0, i.e. hard against the primary's left edge. That is the
    /// "it always opens in the top-left corner" report.</para>
    /// <para>Under per-monitor DPI awareness there is no single px↔DIP factor to invert a DIP point
    /// back to a px point for <c>MonitorFromPoint</c> — each monitor can have its own scale. Instead
    /// this walks every monitor via <c>EnumDisplayMonitors</c>, converts each one's work area to DIP
    /// with ITS OWN DPI, and returns the first whose DIP rect contains the point. If none does (the
    /// point is just off every monitor's edge), it falls back to the DIP rect whose centre is
    /// nearest — the same "nearest monitor" behaviour <c>MONITOR_DEFAULTTONEAREST</c> gives.</para>
    /// </summary>
    internal static Rect MonitorWorkAreaDipAt(double xDip, double yDip)
    {
        try
        {
            var point = new Point(xDip, yDip);
            Rect? containing = null;
            Rect? nearest = null;
            double nearestDistSq = double.MaxValue;

            bool Callback(IntPtr hMonitor, IntPtr hdc, ref NativeMethods.RECT rect, IntPtr data)
            {
                var wa = MonitorWorkAreaDip(hMonitor);
                if (wa.Contains(point)) { containing = wa; return false; }   // stop early — found it
                double cx = wa.Left + wa.Width / 2, cy = wa.Top + wa.Height / 2;
                double distSq = (cx - xDip) * (cx - xDip) + (cy - yDip) * (cy - yDip);
                if (distSq < nearestDistSq) { nearestDistSq = distSq; nearest = wa; }
                return true;
            }

            NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, Callback, IntPtr.Zero);
            if (containing is { } c) return c;
            if (nearest is { } n) return n;
        }
        catch { /* fall through to the primary work area */ }
        return SystemParameters.WorkArea;
    }

    /// <summary>Work area of the monitor under the mouse, in <paramref name="w"/>'s DIP space.
    /// Used so a float-ball / tray / hotkey summon on a secondary monitor opens there.</summary>
    internal static Rect CursorWorkArea(Window w) => MonitorWorkAreaFor(w, CursorMonitor());

    /// <summary>Destination-monitor DIP work area under the cursor (sizes / MaxHeight). Prefer
    /// <see cref="CursorWorkArea(Window)"/> when assigning Left/Top.</summary>
    internal static Rect CursorWorkArea() => MonitorWorkAreaDip(CursorMonitor());

    private static IntPtr CursorMonitor()
    {
        try
        {
            if (NativeMethods.GetCursorPos(out var pt))
                return NativeMethods.MonitorFromPoint(pt, NativeMethods.MONITOR_DEFAULTTONEAREST);
        }
        catch { /* fall through */ }
        return IntPtr.Zero;
    }

    private static bool TryMonitorWorkAreaPx(IntPtr monitor, out NativeMethods.RECT rcWork)
    {
        rcWork = default;
        if (monitor == IntPtr.Zero) return false;
        var mi = new NativeMethods.MONITORINFO
            { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MONITORINFO>() };
        if (!NativeMethods.GetMonitorInfo(monitor, ref mi)) return false;
        rcWork = mi.rcWork;
        return true;
    }

    private static bool TryTransformFromDevice(Window w, out Matrix tfd)
    {
        tfd = default;
        try
        {
            // Pre-created panels call PositionAndSize before Show — EnsureHandle builds the source
            // so TransformFromDevice is available without a visible flash.
            if (new WindowInteropHelper(w).EnsureHandle() == IntPtr.Zero) return false;
            var src = PresentationSource.FromVisual(w);
            if (src?.CompositionTarget is null) return false;
            tfd = src.CompositionTarget.TransformFromDevice;
            return true;
        }
        catch
        {
            return false;
        }
    }
}
