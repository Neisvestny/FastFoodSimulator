using CommunityToolkit.Mvvm.ComponentModel;

namespace FastFoodSimulator.ViewModels;

public partial class CustomerDotViewModel : ViewModelBase
{
    public int CustomerId { get; }
    public int ColorIndex { get; }
    public string Label => $"#{CustomerId}";
    public string Color { get; }

    [ObservableProperty]
    private double _x;

    [ObservableProperty]
    private double _y;

    [ObservableProperty]
    private int _orderNumber;

    // пусто, пока заказ не оформлен
    public string OrderNumberText => OrderNumber == 0 ? string.Empty : $"№{OrderNumber}";

    public CustomerDotViewModel(int customerId, int colorIndex, string color, double x, double y)
    {
        CustomerId = customerId;
        ColorIndex = colorIndex;
        Color = color;
        _x = x;
        _y = y;
    }

    partial void OnOrderNumberChanged(int value) => OnPropertyChanged(nameof(OrderNumberText));
}
