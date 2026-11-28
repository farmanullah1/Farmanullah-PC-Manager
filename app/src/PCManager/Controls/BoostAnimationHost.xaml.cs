using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using PCManager.Core.Formatting;
using PCManager.Core.Models;
using PCManager.Motion;
using MotionKit = PCManager.Motion.Motion;

namespace PCManager.Controls;

/// <summary>
/// Shared Boost animation surface (U7/W5): the same phases, rocket, flame, count-up chip and error
/// shake on Home and in the floating toolbar. Real values only — the chip counts the bytes the
/// service actually removed.
/// </summary>
public partial class BoostAnimationHost : UserControl
{
    public static readonly DependencyProperty PhaseProperty = DependencyProperty.Register(
        nameof(Phase), typeof(BoostPhase), typeof(BoostAnimationHost),
        new PropertyMetadata(BoostPhase.Idle, OnPhaseChanged));

    public static readonly DependencyProperty HeadlineProperty = DependencyProperty.Register(
        nameof(Headline), typeof(string), typeof(BoostAnimationHost),
        new PropertyMetadata(string.Empty, OnHeadlineChanged));

    public static readonly DependencyProperty ResultTextProperty = DependencyProperty.Register(
        nameof(ResultText), typeof(string), typeof(BoostAnimationHost),
        new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty FreedBytesProperty = DependencyProperty.Register(
        nameof(FreedBytes), typeof(long), typeof(BoostAnimationHost),
        new PropertyMetadata(0L, OnFreedChanged));

    public static readonly DependencyProperty IsCompactProperty = DependencyProperty.Register(
        nameof(IsCompact), typeof(bool), typeof(BoostAnimationHost),
        new PropertyMetadata(false, OnCompactChanged));

    public static readonly DependencyProperty CancelCommandProperty = DependencyProperty.Register(
        nameof(CancelCommand), typeof(ICommand), typeof(BoostAnimationHost),
        new PropertyMetadata(null, OnCancelCommandChanged));

    private readonly TranslateTransform _rocketShift = new();
    private readonly ScaleTransform _flameScale = new(1, 1);
    private readonly DispatcherTimer _countUp;
    private long _target;
    private long _shown;

    public BoostAnimationHost()
    {
        InitializeComponent();

        Rocket.RenderTransform = _rocketShift;
        Flame.RenderTransform = _flameScale;

        _countUp = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
        _countUp.Tick += (_, _) => StepCountUp();
    }

    public BoostPhase Phase
    {
        get => (BoostPhase)GetValue(PhaseProperty);
        set => SetValue(PhaseProperty, value);
    }

    public string Headline
    {
        get => (string)GetValue(HeadlineProperty);
        set => SetValue(HeadlineProperty, value);
    }

    public string ResultText
    {
        get => (string)GetValue(ResultTextProperty);
        set => SetValue(ResultTextProperty, value);
    }

    public long FreedBytes
    {
        get => (long)GetValue(FreedBytesProperty);
        set => SetValue(FreedBytesProperty, value);
    }

    public bool IsCompact
    {
        get => (bool)GetValue(IsCompactProperty);
        set => SetValue(IsCompactProperty, value);
    }

    public ICommand? CancelCommand
    {
        get => (ICommand?)GetValue(CancelCommandProperty);
        set => SetValue(CancelCommandProperty, value);
    }

    private static void OnPhaseChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((BoostAnimationHost)d).ApplyPhase((BoostPhase)e.NewValue);

    private static void OnHeadlineChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var host = (BoostAnimationHost)d;
        host.HeadlineText.Text = (string)e.NewValue;
    }

    private static void OnFreedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var host = (BoostAnimationHost)d;
        host._target = (long)e.NewValue;
        host._shown = 0;

        // Binding order is not guaranteed: when the result arrives after the phase, the count-up
        // must still run against the real byte count instead of leaving "0 B freed" on screen.
        if (host.Phase == BoostPhase.Completed && host.IsLoaded)
        {
            host.StartCountUp();
        }
    }

    private static void OnCompactChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var host = (BoostAnimationHost)d;
        double scale = (bool)e.NewValue ? 0.6 : 1.0;
        host.LayoutTransform = new ScaleTransform(scale, scale);
    }

    private static void OnCancelCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var host = (BoostAnimationHost)d;
        host.CancelButton.Command = (ICommand?)e.NewValue;
    }

    private void ApplyPhase(BoostPhase phase)
    {
        StopAnimations();

        switch (phase)
        {
            case BoostPhase.Idle:
                Flame.Visibility = Visibility.Collapsed;
                Chip.Visibility = Visibility.Collapsed;
                CancelButton.Visibility = Visibility.Collapsed;
                break;

            case BoostPhase.Measuring:
                Flame.Visibility = Visibility.Collapsed;
                Chip.Visibility = Visibility.Collapsed;
                CancelButton.Visibility = Visibility.Visible;
                Pulse();
                break;

            case BoostPhase.Working:
                Flame.Visibility = Visibility.Visible;
                Chip.Visibility = Visibility.Collapsed;
                CancelButton.Visibility = Visibility.Visible;
                Fly();
                break;

            case BoostPhase.Completed:
                Flame.Visibility = Visibility.Collapsed;
                Chip.Visibility = Visibility.Visible;
                CancelButton.Visibility = Visibility.Collapsed;
                StartCountUp();
                break;

            case BoostPhase.Failed:
                Flame.Visibility = Visibility.Collapsed;
                Chip.Visibility = Visibility.Visible;
                CancelButton.Visibility = Visibility.Collapsed;

                if (MotionKit.Policy.AllowsTransforms)
                {
                    MotionKit.Shake(Rocket);
                }

                break;
        }
    }

    private void Pulse()
    {
        if (!MotionKit.Policy.AllowsFades)
        {
            Rocket.Opacity = 0.75;
            return;
        }

        var pulse = new DoubleAnimation(1, 0.55, TimeSpan.FromMilliseconds(520))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
        };
        Rocket.BeginAnimation(OpacityProperty, pulse);
    }

    private void Fly()
    {
        if (!MotionKit.Policy.AllowsTransforms)
        {
            Flame.Opacity = 0.8;
            return;
        }

        var bob = new DoubleAnimation(0, -3, TimeSpan.FromMilliseconds(420))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
        };
        _rocketShift.BeginAnimation(TranslateTransform.YProperty, bob);

        var flicker = new DoubleAnimation(1, 0.5, TimeSpan.FromMilliseconds(180))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
        };
        Flame.BeginAnimation(OpacityProperty, flicker);

        var stretch = new DoubleAnimation(1, 1.25, TimeSpan.FromMilliseconds(180))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
        };
        _flameScale.BeginAnimation(ScaleTransform.ScaleYProperty, stretch);
    }

    private void StartCountUp()
    {
        if (_target <= 0)
        {
            ChipText.Text = "Trim complete";
            return;
        }

        _shown = 0;
        _countUp.Start();
    }

    private void StepCountUp()
    {
        long step = Math.Max(1, _target / 24);
        _shown = Math.Min(_target, _shown + step);
        ChipText.Text = $"{ByteFormatter.Format(_shown)} freed";

        if (_shown >= _target)
        {
            _countUp.Stop();
            ChipText.Text = $"{ByteFormatter.Format(_target)} freed";
        }
    }

    private void StopAnimations()
    {
        _countUp.Stop();
        Rocket.BeginAnimation(OpacityProperty, null);
        Rocket.Opacity = 1;
        Flame.BeginAnimation(OpacityProperty, null);
        _rocketShift.BeginAnimation(TranslateTransform.YProperty, null);
        _rocketShift.Y = 0;
        _flameScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        _flameScale.ScaleY = 1;
    }
}
