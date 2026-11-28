using System.Windows.Controls;
using System.ComponentModel;
using PCManager.ViewModels;
using PCManager.Motion;
using MotionKit = PCManager.Motion.Motion;

namespace PCManager.Views;

public partial class DebloatView : UserControl
{
    private DebloatViewModel? _viewModel;

    public DebloatView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        IsVisibleChanged += (_, _) =>
        {
            if (DataContext is DebloatViewModel viewModel)
            {
                viewModel.SetPageVisible(IsVisible);
            }
        };
    }

    private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = e.NewValue as DebloatViewModel;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            ApplyRunningMotion(_viewModel.IsRunning);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DebloatViewModel.IsRunning))
        {
            ApplyRunningMotion(_viewModel?.IsRunning == true);
        }
    }

    private void ApplyRunningMotion(bool running)
    {
        if (running)
        {
            MotionKit.StartShimmer(DebloatRunningIcon);
            MotionKit.FadeIn(DebloatRunningCard, TimeSpan.FromMilliseconds(140));
        }
        else
        {
            MotionKit.StopShimmer(DebloatRunningIcon);
        }
    }
}
