using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using PCManager.Motion;
using PCManager.ViewModels;
using MotionKit = PCManager.Motion.Motion;

namespace PCManager.Views;

public partial class StorageView : UserControl
{
    private StorageViewModel? _viewModel;

    public StorageView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel.CleanupItems.CollectionChanged -= OnCleanupItemsChanged;
        }

        _viewModel = e.NewValue as StorageViewModel;
        if (_viewModel is null)
        {
            return;
        }

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _viewModel.CleanupItems.CollectionChanged += OnCleanupItemsChanged;
        ApplyBusyMotion(_viewModel.IsBusy);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(StorageViewModel.IsBusy) &&
            sender is StorageViewModel viewModel)
        {
            ApplyBusyMotion(viewModel.IsBusy);
        }
    }

    private void ApplyBusyMotion(bool busy)
    {
        if (busy)
        {
            MotionKit.StartShimmer(CleanupScanIcon);
            MotionKit.StartShimmer(StorageStatusText);
        }
        else
        {
            MotionKit.StopShimmer(CleanupScanIcon);
            MotionKit.StopShimmer(StorageStatusText);
        }
    }

    private void OnCleanupItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            var elements = new List<FrameworkElement>();
            for (int index = 0; index < CleanupItemsList.Items.Count && elements.Count < 12; index++)
            {
                if (CleanupItemsList.ItemContainerGenerator.ContainerFromIndex(index) is FrameworkElement element)
                {
                    elements.Add(element);
                }
            }
            MotionKit.Stagger(elements);
        });
    }
}
