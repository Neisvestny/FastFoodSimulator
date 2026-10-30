using Avalonia.Controls;
using FastFoodSimulator.ViewModels;

namespace FastFoodSimulator.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnSceneSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.OnSceneSizeChanged(e.NewSize.Width, e.NewSize.Height);
        }
    }
}
