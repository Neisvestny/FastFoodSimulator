using CommunityToolkit.Mvvm.ComponentModel;

namespace FastFoodSimulator.ViewModels;

public partial class TicketDotViewModel : ViewModelBase
{
    public int OrderNumber { get; }
    public string Label => OrderNumber.ToString();

    [ObservableProperty]
    private string _state = "Waiting";

    partial void OnStateChanged(string value) => OnPropertyChanged(nameof(Brush));

    [ObservableProperty]
    private double _x;

    [ObservableProperty]
    private double _y;

    public TicketDotViewModel(int orderNumber, double x, double y)
    {
        OrderNumber = orderNumber;
        _x = x;
        _y = y;
    }

    public string Brush => State switch
    {
        "Cooking" => "#E67E22",
        "Carried" => "#1ABC9C",
        "OnCounter" => "#2ECC71",
        _ => "#F1C40F"
    };
}
