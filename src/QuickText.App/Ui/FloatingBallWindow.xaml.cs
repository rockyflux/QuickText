using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using QuickText.App.Interop;
using QuickText.Core.Localization;

namespace QuickText.App.Ui;

/// <summary>Always-on-top summon affordance: left-click toggles search, drag remembers position,
/// right-click shares the tray menu plus hide-for-this-session.</summary>
public partial class FloatingBallWindow : Window
{
    private const double DragThresholdPx = 4;
    private const double SizeDip = 52;
    private const double DefaultMargin = 24;

    private Point _grabOffset;   // mouse offset inside the ball (DIPs)
    private Point _downScreenPx;
    private bool _dragging;
    private bool _moved;

    public FloatingBallWindow()
    {
        InitializeComponent();
        WindowTheming.ApplyFlowDirection(this);
        ApplyPositionFromSettings();
        // Clicks must not activate this window: activation would yank foreground off the panel
        // (or the user's app) and the panel's Deactivated auto-hide reads that as "user left".
        SourceInitialized += (_, _) =>
        {
            if (PresentationSource.FromVisual(this) is HwndSource src)
                src.AddHook(WndProc);
        };
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_MOUSEACTIVATE)
        {
            handled = true;
            return new IntPtr(NativeMethods.MA_NOACTIVATE);
        }
        return IntPtr.Zero;
    }

    /// <summary>Refresh headers so pause / language changes match the tray.</summary>
    public void ApplyMenuLabels()
    {
        if (ContextMenu is not { } menu) return;
        var loc = LocalizationService.Instance;
        bool paused = !AppState.Current.Settings.AbbrEnabled;
        menu.FlowDirection = loc.Culture.TextInfo.IsRightToLeft
            ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        ((MenuItem)menu.Items[0]).Header = loc["Tray.OpenSearch"];
        ((MenuItem)menu.Items[1]).Header = loc["Tray.OpenManager"];
        ((MenuItem)menu.Items[2]).Header = loc["Tray.OpenCategories"];
        ((MenuItem)menu.Items[3]).Header = loc["Tray.OpenSkills"];
        ((MenuItem)menu.Items[4]).Header = loc["Tray.NewFromClipboard"];
        ((MenuItem)menu.Items[5]).Header = loc["Tray.Settings"];
        var pauseItem = (MenuItem)menu.Items[6];
        pauseItem.Header = loc[paused ? "Tray.ResumeAbbr" : "Tray.PauseAbbr"];
        if (pauseItem.Icon is TextBlock ico)
            ico.Text = paused ? "\uE768" : "\uE769";
        // Items[7] = separator; [8] = hide ball; [9] = separator; [10] = Exit.
        ((MenuItem)menu.Items[8]).Header = loc["Tray.HideFloatBall"];
        ((MenuItem)menu.Items[10]).Header = loc["Tray.Exit"];
    }

    private void OnMenuOpened(object sender, RoutedEventArgs e) => ApplyMenuLabels();

    private void ApplyPositionFromSettings()
    {
        if (App.InSmoke) return;
        var s = AppState.Current.Settings;
        bool remembered = s.FloatBallX != 0 || s.FloatBallY != 0;
        if (remembered)
        {
            var wa = WindowTheming.MonitorWorkAreaDipAt(s.FloatBallX, s.FloatBallY);
            Left = Clamp(s.FloatBallX, wa.Left, wa.Right - SizeDip);
            Top = Clamp(s.FloatBallY, wa.Top, wa.Bottom - SizeDip);
        }
        else
        {
            var wa = SystemParameters.WorkArea;
            Left = wa.Right - SizeDip - DefaultMargin;
            Top = wa.Bottom - SizeDip - DefaultMargin;
        }
    }

    private void PersistPosition()
    {
        var wa = WindowTheming.MonitorWorkAreaDipAt(Left, Top);
        Left = Clamp(Left, wa.Left, wa.Right - SizeDip);
        Top = Clamp(Top, wa.Top, wa.Bottom - SizeDip);
        var s = AppState.Current.Settings;
        if (s.FloatBallX == Left && s.FloatBallY == Top) return;
        s.FloatBallX = Left;
        s.FloatBallY = Top;
        try { AppState.Current.SettingsStore.Save(s); } catch { /* keep UI movable if save fails */ }
    }

    private static double Clamp(double v, double min, double max) =>
        Math.Max(min, Math.Min(v, max));

    private void OnBallMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        _grabOffset = e.GetPosition(this);
        _downScreenPx = PointToScreen(_grabOffset);
        _dragging = true;
        _moved = false;
        Ball.CaptureMouse();
        e.Handled = true;
    }

    private void OnBallMouseMove(object sender, MouseEventArgs e)
    {
        if (!_dragging || e.LeftButton != MouseButtonState.Pressed) return;
        var screenPx = PointToScreen(e.GetPosition(this));
        double dxPx = screenPx.X - _downScreenPx.X;
        double dyPx = screenPx.Y - _downScreenPx.Y;
        if (!_moved && Math.Abs(dxPx) < DragThresholdPx && Math.Abs(dyPx) < DragThresholdPx) return;
        _moved = true;
        // Device px → DIP via this window's composition transform (PerMonitorV2-safe).
        var source = PresentationSource.FromVisual(this);
        if (source?.CompositionTarget is null) return;
        var tipDip = source.CompositionTarget.TransformFromDevice.Transform(screenPx);
        Left = tipDip.X - _grabOffset.X;
        Top = tipDip.Y - _grabOffset.Y;
    }

    private void OnBallMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || !_dragging) return;
        _dragging = false;
        Ball.ReleaseMouseCapture();
        if (_moved) PersistPosition();
        else ((App)Application.Current).ToggleSearchFromFloatBall();
        e.Handled = true;
    }

    private App AppHost => (App)Application.Current;

    private void OnOpenSearch(object s, RoutedEventArgs e) => AppHost.OpenSearchFromMenu();
    private void OnOpenManager(object s, RoutedEventArgs e) => AppHost.OpenManagerFromMenu();
    private void OnOpenCategoryManager(object s, RoutedEventArgs e) => AppHost.OpenCategoryManagerFromMenu();
    private void OnOpenSkills(object s, RoutedEventArgs e) => AppHost.OpenSkillsFromMenu();
    private void OnNewFromClipboard(object s, RoutedEventArgs e) => AppHost.NewFromClipboardFromMenu();
    private void OnSettings(object s, RoutedEventArgs e) => AppHost.OpenSettingsFromMenu();
    private void OnTogglePause(object s, RoutedEventArgs e) => AppHost.TogglePauseFromMenu();
    private void OnHide(object s, RoutedEventArgs e) => AppHost.HideFloatBall();
    private void OnExit(object s, RoutedEventArgs e) => AppHost.ExitFromMenu();
}
