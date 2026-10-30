using CommunityToolkit.Mvvm.ComponentModel;

namespace FastFoodSimulator.ViewModels;

public partial class StaffViewModel : ViewModelBase
{
    public string Role { get; }
    public int Index { get; }
    public string Label { get; }
    public string Icon { get; }
    public string Color { get; }

    public double HomeX { get; }
    public double HomeY { get; }

    public double BusyX { get; }
    public double BusyY { get; }

    [ObservableProperty]
    private double _x;

    [ObservableProperty]
    private double _y;

    [ObservableProperty]
    private bool _isActive;

    public double Scale => IsActive ? 1.25 : 1.0;

    public StaffViewModel(
        string role, int index, string label, string icon, string color,
        double homeX, double homeY, double busyX, double busyY)
    {
        Role = role;
        Index = index;
        Label = label;
        Icon = icon;
        Color = color;
        HomeX = homeX;
        HomeY = homeY;
        BusyX = busyX;
        BusyY = busyY;
        _x = homeX;
        _y = homeY;
    }

    partial void OnIsActiveChanged(bool value) => OnPropertyChanged(nameof(Scale));
}
