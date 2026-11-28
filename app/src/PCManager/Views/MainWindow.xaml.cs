using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Media;
using System.Windows.Threading;
using PCManager.Core;
using PCManager.Core.Models;
using PCManager.Motion;
using PCManager.Services;
using PCManager.Services.Desktop;
using MotionKit = PCManager.Motion.Motion;
using PCManager.Services.Abstractions;
using PCManager.ViewModels;

namespace PCManager.Views;

/// <summary>
/// Main Shell Window for FPM (Card D2, §8.4).
/// Provides responsive breakpoints (360px - 1440+ px), custom WindowChrome,
/// snap layout support, and window position persistence.
/// </summary>
public partial class MainWindow : FpmWindow
{
    private readonly MainViewModel _viewModel;
    private readonly ISettingsService _settings;
    private readonly IAppLogger _logger;
    private readonly FrameTimingProbe _frameProbe = new();
    private readonly Dictionary<object, FrameworkElement> _pageViews = new();
    private readonly Dictionary<ViewModelBase, double> _scrollOffsets = new();
    private FrameworkElement? _visiblePage;
    private DesktopFollowLoop? _desktopFollower;
    private readonly DispatcherTimer _smoothScrollTimer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private ScrollViewer? _smoothScrollViewer;
    private ScrollViewer? _backToTopScrollViewer;
    private double _smoothScrollTarget;
    private double _smoothScrollVelocity;

    public MainWindow(
        MainViewModel viewModel,
        ISettingsService settings,
        IAppLogger logger,
        BatteryStatusViewModel battery)
    {
        InitializeComponent();

        // Lets Windows show Snap Layouts when the pointer rests on our custom maximize button.
        MaximizeHitTarget = MaximizeButton;

        logger.Info(
            $"Render tier {RenderCapability.Tier >> 16}; DWM composition " +
            $"{(FpmWindow.IsCompositionEnabled() ? "on" : "off")}.");

        _viewModel = viewModel;
        _settings = settings;
        _logger = logger;
        DataContext = viewModel;

        _smoothScrollTimer.Tick += OnSmoothScrollTick;

        RestoreWindowBounds();

        SourceInitialized += (_, _) => StartDesktopFollower();
        Closed += (_, _) => _desktopFollower?.Dispose();

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        // T1: the shared sampler runs at 5 s only while at least one surface is visible.
        IsVisibleChanged += (_, _) => battery.SetVisible("main", IsVisible);

        Loaded += OnLoaded;
        SizeChanged += OnWindowSizeChanged;
        StateChanged += OnWindowStateChanged;
        Closing += OnWindowClosing;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        UpdateLayoutForWidth(ActualWidth);

        FrameworkElement initial = GetPageView(_viewModel.CurrentPage);
        initial.Visibility = Visibility.Visible;
        initial.Opacity = 1;
        _visiblePage = initial;

        await Dispatcher.Yield(DispatcherPriority.Loaded);
        UpdateNavPill(animate: false);
        await _viewModel.LoadAsync();
    }

    /// <summary>Puts the caret in the palette search box whenever the palette opens.</summary>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsCommandPaletteOpen))
        {
            if (_viewModel.IsCommandPaletteOpen)
            {
                AnimatePaletteOpen();
                FocusPaletteQuery();
            }

            return;
        }

        if (e.PropertyName == nameof(MainViewModel.CurrentPage))
        {
            _frameProbe.Measure(MotionPolicy.PageDuration + TimeSpan.FromMilliseconds(80), _logger.Info);
            AnimatePageChange();
            return;
        }

        if (e.PropertyName == nameof(MainViewModel.IsExpandedSidebar))
        {
            Dispatcher.BeginInvoke(new Action(() => UpdateNavPill(animate: false)), DispatcherPriority.Loaded);
            return;
        }

        // A new query means a new result list; keep the highlight on the first match.
        if (e.PropertyName == nameof(MainViewModel.CommandPaletteQuery) && _viewModel.IsCommandPaletteOpen)
        {
            Dispatcher.BeginInvoke(new Action(SelectFirstMatch), DispatcherPriority.Input);
        }
    }

    /// <summary>
    /// Shared-axis page change (U6): the outgoing page fades out in 120 ms while the cached
    /// incoming page fades in and slides 16 px from the direction of travel. Reduced motion
    /// keeps the cross-fade; motion off swaps instantly. Cached views keep their scroll offset.
    /// </summary>
    private void AnimatePageChange()
    {
        FrameworkElement next = GetPageView(_viewModel.CurrentPage);
        FrameworkElement? previous = _visiblePage;

        if (ReferenceEquals(previous, next))
        {
            return;
        }

        _visiblePage = next;
        SaveScrollOffset(previous);
        RestoreScrollOffset(next);

        bool movingDown = previous is null
            || IndexOfPage(next.DataContext) >= IndexOfPage(previous.DataContext);

        Panel.SetZIndex(next, 1);
        next.Visibility = Visibility.Visible;

        var nextShift = (TranslateTransform)next.RenderTransform;
        double distance = MotionKit.Policy.AllowsTransforms ? 16d : 0d;
        var enterMotion = TimeSpan.FromMilliseconds(MotionPolicy.PageDuration.TotalMilliseconds);
        var exitMotion = TimeSpan.FromMilliseconds(120);
        var enterEase = new CubicEase { EasingMode = EasingMode.EaseOut };
        var exitEase = new CubicEase { EasingMode = EasingMode.EaseIn };

        if (!MotionKit.Policy.AllowsFades)
        {
            next.BeginAnimation(UIElement.OpacityProperty, null);
            next.Opacity = 1;
            nextShift.BeginAnimation(TranslateTransform.YProperty, null);
            nextShift.Y = 0;
            Collapse(previous);
            Dispatcher.BeginInvoke(new Action(() => UpdateNavPill(animate: false)), DispatcherPriority.Loaded);
            return;
        }

        next.Opacity = 0;
        nextShift.Y = movingDown ? distance : -distance;
        next.BeginAnimation(
            UIElement.OpacityProperty,
            new DoubleAnimation(0d, 1d, enterMotion) { EasingFunction = enterEase });

        if (distance > 0)
        {
            nextShift.BeginAnimation(
                TranslateTransform.YProperty,
                new DoubleAnimation(nextShift.Y, 0d, enterMotion) { EasingFunction = enterEase });
        }
        else
        {
            nextShift.Y = 0;
        }

        if (previous is not null)
        {
            Panel.SetZIndex(previous, 0);
            var previousShift = (TranslateTransform)previous.RenderTransform;
            var exit = new DoubleAnimation(1d, 0d, exitMotion) { EasingFunction = exitEase };
            exit.Completed += (_, _) => Collapse(previous);
            previous.BeginAnimation(UIElement.OpacityProperty, exit);

            if (distance > 0)
            {
                previousShift.BeginAnimation(
                    TranslateTransform.YProperty,
                    new DoubleAnimation(0d, movingDown ? -8d : 8d, exitMotion) { EasingFunction = exitEase });
            }
        }

        Dispatcher.BeginInvoke(new Action(() => UpdateNavPill(animate: true)), DispatcherPriority.Loaded);
    }

    private static void Collapse(FrameworkElement? page)
    {
        if (page is null)
        {
            return;
        }

        page.BeginAnimation(UIElement.OpacityProperty, null);
        page.Visibility = Visibility.Collapsed;
        page.Opacity = 1;

        if (page.RenderTransform is TranslateTransform shift)
        {
            shift.BeginAnimation(TranslateTransform.YProperty, null);
            shift.Y = 0;
        }
    }

    /// <summary>
        /// Builds every page view at idle time, one per dispatcher turn, so the first visit to a
        /// page does not pay the whole construction cost inside its transition.
        /// </summary>
    public void PrewarmPages()
    {
        List<ViewModelBase> pages = _viewModel.Pages
            .Concat(_viewModel.SettingsItems)
            .Select(item => item.Page)
            .ToList();

        Dispatcher.BeginInvoke(new Action(() => PrewarmNext(pages, 0)), DispatcherPriority.ApplicationIdle);
    }

    private void PrewarmNext(List<ViewModelBase> pages, int index)
    {
        if (index >= pages.Count)
        {
            return;
        }

        GetPageView(pages[index]);
        Dispatcher.BeginInvoke(new Action(() => PrewarmNext(pages, index + 1)), DispatcherPriority.ApplicationIdle);
    }

    /// <summary>One visual per page, created once and reused for the life of the window.</summary>
    private FrameworkElement GetPageView(object page)
    {
        if (_pageViews.TryGetValue(page, out FrameworkElement? cached))
        {
            return cached;
        }

        UserControl view = page switch
        {
            HomeViewModel => new HomeView(),
            MemoryViewModel => new MemoryView(),
            ProtectionViewModel => new ProtectionView(),
            StorageViewModel => new StorageView(),
            AppsViewModel => new AppsView(),
            ToolboxViewModel => new ToolboxView(),
            DebloatViewModel => new DebloatView(),
            SettingsViewModel => new SettingsView(),
            _ => throw new InvalidOperationException($"No view is registered for {page.GetType().Name}.")
        };

        view.DataContext = page;
        view.Visibility = Visibility.Collapsed;
        view.Opacity = 0;
        view.RenderTransform = new TranslateTransform();

        PageHost.Children.Add(view);
        _pageViews[page] = view;
        return view;
    }

    private int IndexOfPage(object? page)
    {
        if (page is null)
        {
            return 0;
        }

        int index = 0;

        foreach (NavigationItem item in _viewModel.Pages.Concat(_viewModel.SettingsItems))
        {
            if (ReferenceEquals(item.Page, page))
            {
                return index;
            }

            index++;
        }

        return index;
    }

    /// <summary>Places the rail's selection pill behind the selected item and slides it there.</summary>
    private void UpdateNavPill(bool animate)
    {
        ListBoxItem? container =
            PrimaryNav.ItemContainerGenerator.ContainerFromItem(_viewModel.SelectedPage) as ListBoxItem
            ?? SettingsNav.ItemContainerGenerator.ContainerFromItem(_viewModel.SelectedPage) as ListBoxItem;

        if (container is null || container.ActualHeight <= 0)
        {
            return;
        }

        Point origin = container.TranslatePoint(new Point(0, 0), NavPillLayer);
        NavPill.Width = Math.Max(0, container.ActualWidth);
        NavPill.Height = container.ActualHeight;
        NavPill.Opacity = 1;

        if (animate)
        {
            MotionKit.MoveTo(NavPillShift, toX: 0, toY: origin.Y, TimeSpan.FromMilliseconds(180));
            return;
        }

        NavPillShift.BeginAnimation(TranslateTransform.XProperty, null);
        NavPillShift.BeginAnimation(TranslateTransform.YProperty, null);
        NavPillShift.Y = origin.Y;
    }

    /// <summary>Fades and zooms the palette panel in through the motion kit.</summary>
    private void AnimatePaletteOpen()
    {
        MotionKit.FadeIn(PaletteOverlay, MotionPolicy.PaletteDuration);
        MotionKit.ScaleIn(PaletteScale, from: 0.98, MotionPolicy.PaletteDuration);
    }

    private void FocusPaletteQuery()
    {
        Dispatcher.BeginInvoke(
            new Action(() =>
            {
                PaletteQuery.Focus();
                Keyboard.Focus(PaletteQuery);
                PaletteQuery.SelectAll();
                SelectFirstMatch();
            }),
            DispatcherPriority.Input);
    }

    private void SelectFirstMatch()
    {
        if (PaletteList.Items.Count == 0)
        {
            PaletteList.SelectedIndex = -1;
            return;
        }

        PaletteList.SelectedIndex = 0;
        PaletteList.ScrollIntoView(PaletteList.Items[0]);
    }

    /// <summary>Escape closes the palette; arrows move the highlight; Enter runs the entry.</summary>
    private void Window_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.XButton1)
        {
            _viewModel.GoBackCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.ChangedButton == MouseButton.XButton2)
        {
            _viewModel.GoForwardCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void Window_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        DependencyObject? source = e.OriginalSource as DependencyObject;
        if (source is not null && HasAncestor<ComboBox>(source))
        {
            return;
        }
        if (source is not null && HasAncestor<Slider>(source))
        {
            return;
        }

        ScrollViewer? scroll = source is null
            ? null
            : FindContainingScrollViewer(source);

        if (scroll is null)
        {
            return;
        }

        bool atStart = e.Delta > 0 && scroll.VerticalOffset <= 0;
        bool atEnd = e.Delta < 0 &&
                     scroll.VerticalOffset >= scroll.ScrollableHeight;

        if (atStart || atEnd)
        {
            return;
        }

        e.Handled = true;
        _smoothScrollViewer = scroll;
        _smoothScrollTarget = Math.Clamp(
            scroll.VerticalOffset - (e.Delta * 0.45),
            0,
            scroll.ScrollableHeight);
        _smoothScrollVelocity = (_smoothScrollTarget - scroll.VerticalOffset) * 0.28;
        _smoothScrollTimer.Start();
    }

    private void OnSmoothScrollTick(object? sender, EventArgs e)
    {
        if (_smoothScrollViewer is null)
        {
            _smoothScrollTimer.Stop();
            return;
        }

        double remaining = _smoothScrollTarget - _smoothScrollViewer.VerticalOffset;
        if (Math.Abs(remaining) < 1)
        {
            _smoothScrollViewer.ScrollToVerticalOffset(_smoothScrollTarget);
            _smoothScrollTimer.Stop();
            _smoothScrollViewer = null;
            return;
        }

        _smoothScrollVelocity = (_smoothScrollVelocity + remaining * 0.18) * 0.72;
        _smoothScrollViewer.ScrollToVerticalOffset(
            Math.Clamp(
                _smoothScrollViewer.VerticalOffset + _smoothScrollVelocity,
                0,
                _smoothScrollViewer.ScrollableHeight));
    }

    private static ScrollViewer? FindContainingScrollViewer(DependencyObject source)
    {
        DependencyObject? current = source;
        while (current is not null)
        {
            if (current is ScrollViewer scroll)
            {
                return scroll;
            }
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }

    private static bool HasAncestor<T>(DependencyObject source)
        where T : DependencyObject
    {
        DependencyObject? current = source;
        while (current is not null)
        {
            if (current is T)
            {
                return true;
            }
            current = VisualTreeHelper.GetParent(current);
        }
        return false;
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Alt &&
            e.SystemKey == Key.Left)
        {
            _viewModel.GoBackCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Alt &&
            e.SystemKey == Key.Right)
        {
            _viewModel.GoForwardCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            string? key = e.Key switch
            {
                Key.D1 => PageKeys.Home,
                Key.D2 => PageKeys.Memory,
                Key.D3 => PageKeys.Protection,
                Key.D4 => PageKeys.Storage,
                Key.D5 => PageKeys.Apps,
                Key.D6 => PageKeys.Toolbox,
                Key.D7 => PageKeys.Debloat,
                _ => null,
            };

            if (key is not null)
            {
                _viewModel.SelectedPage =
                    _viewModel.Pages.First(item => item.Key == key);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.OemComma)
            {
                _viewModel.SelectedPage =
                    _viewModel.SettingsItems.First();
                e.Handled = true;
                return;
            }
        }

        if (e.Key == Key.F5)
        {
            _viewModel.RefreshCurrentCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (!_viewModel.IsCommandPaletteOpen)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Escape:
                _viewModel.CloseCommandPaletteCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.Down:
                MoveSelection(1);
                e.Handled = true;
                break;

            case Key.Up:
                MoveSelection(-1);
                e.Handled = true;
                break;

            case Key.Enter:
                RunSelectedEntry();
                e.Handled = true;
                break;
        }
    }

    private void MoveSelection(int delta)
    {
        int count = PaletteList.Items.Count;

        if (count == 0)
        {
            return;
        }

        int next = PaletteList.SelectedIndex < 0
            ? (delta > 0 ? 0 : count - 1)
            : Math.Clamp(PaletteList.SelectedIndex + delta, 0, count - 1);

        PaletteList.SelectedIndex = next;
        PaletteList.ScrollIntoView(PaletteList.Items[next]);
    }

    private void RunSelectedEntry()
    {
        object? selection = PaletteList.SelectedItem ??
                            (PaletteList.Items.Count > 0 ? PaletteList.Items[0] : null);

        if (selection is CommandPaletteEntry entry)
        {
            _viewModel.ExecutePaletteCommand.Execute(entry);
        }
    }

    private void SaveScrollOffset(FrameworkElement? page)
    {
        if (page?.DataContext is not ViewModelBase viewModel)
        {
            return;
        }

        ScrollViewer? scroll = FindScrollViewer(page);
        if (scroll is not null)
        {
            _scrollOffsets[viewModel] = scroll.VerticalOffset;
        }
    }

    private void RestoreScrollOffset(FrameworkElement page)
    {
        if (page.DataContext is not ViewModelBase viewModel)
        {
            return;
        }

        ScrollViewer? scroll = FindScrollViewer(page);
        if (scroll is not null &&
            _scrollOffsets.TryGetValue(viewModel, out double offset))
        {
            Dispatcher.BeginInvoke(
                new Action(() => scroll.ScrollToVerticalOffset(offset)),
                DispatcherPriority.Loaded);
        }

        AttachBackToTop(scroll);
    }

    private void AttachBackToTop(ScrollViewer? scroll)
    {
        if (_backToTopScrollViewer is not null)
        {
            _backToTopScrollViewer.ScrollChanged -= OnPageScrollChanged;
        }

        _backToTopScrollViewer = scroll;
        if (_backToTopScrollViewer is null)
        {
            BackToTopButton.Visibility = Visibility.Collapsed;
            return;
        }

        _backToTopScrollViewer.ScrollChanged += OnPageScrollChanged;
        BackToTopButton.Visibility =
            _backToTopScrollViewer.VerticalOffset >= 600
                ? Visibility.Visible
                : Visibility.Collapsed;
    }

    private void OnPageScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        BackToTopButton.Visibility = e.VerticalOffset >= 600
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void BackToTop_Click(object sender, RoutedEventArgs e)
    {
        _backToTopScrollViewer?.ScrollToHome();
    }

    private static ScrollViewer? FindScrollViewer(DependencyObject root)
    {
        if (root is ScrollViewer direct)
        {
            return direct;
        }

        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            ScrollViewer? found = FindScrollViewer(
                VisualTreeHelper.GetChild(root, index));
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private void StartDesktopFollower()
    {
        IntPtr handle = new WindowInteropHelper(this).Handle;
        _desktopFollower = DesktopFollowLoop.Start(
            () => handle,
            () => _settings.Current.MainWindowFollowDesktops && IsVisible && WindowState != WindowState.Minimized,
            _logger);
    }

    private void RestoreWindowBounds()
    {
        Rect work = SystemParameters.WorkArea;
        AppSettings cfg = _settings.Current;
        (string device, uint dpi) = MonitorIdentity.Primary();

        // W1: exact monitor+DPI placement, else the same monitor at another DPI.
        WindowBounds? saved = WindowBoundsTable.Resolve(cfg.WindowBoundsByMonitor, device, dpi);

        WindowBounds desired = saved ?? new WindowBounds
        {
            Width = cfg.MainWindowWidth > 0 ? cfg.MainWindowWidth : 560,
            Height = cfg.MainWindowHeight > 0 ? cfg.MainWindowHeight : 780,
            Left = cfg.MainWindowLeft ?? Math.Max(work.Left, work.Right - 576),
            Top = cfg.MainWindowTop ?? Math.Max(work.Top, work.Bottom - 796),
            Maximized = saved is null && cfg.MainWindowMaximized,
        };

        WindowBounds clamped = WindowBoundsTable.Clamp(
            desired, work.Left, work.Top, work.Width, work.Height, MinWidth, MinHeight);

        Width = clamped.Width;
        Height = clamped.Height;
        Left = clamped.Left;
        Top = clamped.Top;

        if (clamped.Maximized)
        {
            WindowState = WindowState.Maximized;
        }
    }

    private void SaveWindowBounds()
    {
        IntPtr handle = new WindowInteropHelper(this).Handle;

        if (handle == IntPtr.Zero)
        {
            return;
        }

        Rect placement = WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;
        (string device, uint dpi) = MonitorIdentity.ForWindow(handle);

        _settings.Current.WindowBoundsByMonitor[WindowBoundsTable.Key(device, dpi)] = new WindowBounds
        {
            Left = placement.Left,
            Top = placement.Top,
            Width = placement.Width,
            Height = placement.Height,
            Maximized = WindowState == WindowState.Maximized,
        };
    }

    private void OnWindowSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateLayoutForWidth(e.NewSize.Width);
        Dispatcher.BeginInvoke(new Action(() => UpdateNavPill(animate: false)), DispatcherPriority.Loaded);
    }

    private void UpdateLayoutForWidth(double width)
    {
        if (width < 480)
        {
            RailColumn.Width = new GridLength(52);
        }
        else if (width < 960)
        {
            RailColumn.Width = new GridLength(68);
        }
        else
        {
            RailColumn.Width = new GridLength(210);
        }

        _viewModel.UpdateResponsiveLayout(width);
    }

    private void OnWindowStateChanged(object? sender, EventArgs e)
    {
        MaximizeIconText.Text = WindowState == WindowState.Maximized ? "\u274F" : "\u25A2";
    }

    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        SaveWindowBounds();
        _settings.Save();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleMaximize();
            return;
        }

        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void Pin_Click(object sender, RoutedEventArgs e)
    {
        Topmost = !Topmost;
        PinButton.Background = Topmost
            ? (Brush)FindResource("App.AccentSoft")
            : Brushes.Transparent;
        PinIcon.Fill = Topmost
            ? (Brush)FindResource("App.Accent")
            : (Brush)FindResource("App.Subtle");
        PinButton.ToolTip = Topmost ? "Unpin from top" : "Pin on top";
    }

    private void Minimise_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    private void Maximize_Click(object sender, RoutedEventArgs e)
        => ToggleMaximize();

    private void ToggleMaximize()
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
        => Close();
}
