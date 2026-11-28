using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using PCManager.Motion;
using PCManager.ViewModels;
using MotionKit = PCManager.Motion.Motion;

namespace PCManager.Views;

public partial class MemoryView : UserControl
{
    private MemoryViewModel? _viewModel;

    public MemoryView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel.Processes.CollectionChanged -= OnProcessesChanged;
        }

        _viewModel = e.NewValue as MemoryViewModel;
        if (_viewModel is null)
        {
            return;
        }

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _viewModel.Processes.CollectionChanged += OnProcessesChanged;
        ApplyLoad(_viewModel.LoadPercent);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MemoryViewModel.LoadPercent) &&
            sender is MemoryViewModel viewModel)
        {
            ApplyLoad(viewModel.LoadPercent);
        }
    }

    private void ApplyLoad(uint loadPercent)
    {
        MotionKit.ProgressTween(
            (ProgressBar)FindName("MemoryLoadProgress"),
            loadPercent,
            TimeSpan.FromMilliseconds(200));
    }

    private void OnProcessesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            var elements = new List<FrameworkElement>();
            for (int index = 0;
                 index < MemoryProcessList.Items.Count && elements.Count < 12;
                 index++)
            {
                if (MemoryProcessList.ItemContainerGenerator.ContainerFromIndex(index)
                    is FrameworkElement element)
                {
                    elements.Add(element);
                }
            }
            MotionKit.Stagger(elements);
        });
    }
}
