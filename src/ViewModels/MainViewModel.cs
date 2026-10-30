using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FastFoodSimulator.Core;
using FastFoodSimulator.Validation;

namespace FastFoodSimulator.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private const int MaxStaffPerRole = 5;

    // геометрия сцены в единицах Canvas 1150x480
    private const double SpawnX = -40;
    private const double WalkY = 400;
    private const double OrderLineSlot = 38;
    private const double CashierX = 250;
    private const double CashierY = 380;
    private const double ServingLineSlot = 38;
    private const double PickupX = 980;
    private const double PickupY = 380;
    private const double ExitX = 1100;
    private const double ExitY = 420;

    private readonly record struct QueueRow(double Y, double HeadX, int Count);

    // линия заказов: голова у кассы, ряды вверх
    private static readonly QueueRow[] OrderLineRows =
    {
        new(400, 200, 6),
        new(352, 160, 5),
        new(304, 160, 5),
        new(256, 160, 5),
        new(208, 160, 5),
        new(160, 160, 5),
        new(112, 160, 5),
        new(64, 160, 5),
    };

    // очередь выдачи: голова у стойки, ряды вверх
    private static readonly QueueRow[] ServingLineRows =
    {
        new(400, 900, 8),
        new(352, 860, 7),
        new(304, 860, 7),
        new(256, 860, 7),
        new(208, 860, 7),
        new(160, 860, 7),
        new(112, 860, 7),
        new(64, 860, 7),
    };

    private const double KitchenCarouselY = 170;
    private const double KitchenCarouselStartX = 440;
    private const double KitchenCarouselSlot = 48;
    private const double CookingSpotX = 400;
    private const double CookingSpotY = 90;
    private const double ServerHandoverX = 935;
    private const double ServerHandoverY = 190;
    private const double CounterTicketX = 975;
    private const double CounterTicketY = 250;

    // ряды тикетов на карусели, последние два — переполнение вверх
    private static readonly QueueRow[] TicketRows =
    {
        new(170, 440, 9),
        new(224, 440, 9),
        new(266, 440, 9),
        new(308, 440, 9),
        new(350, 440, 9),
        new(128, 500, 8),
        new(86, 500, 8),
    };

    private const double TicketRowRightLimit = 876;
    private const double OrderLineMinX = 4;
    private const double ServingLineMinX = 616;

    // домашние и рабочие позиции, сотрудники расходятся веером вокруг базы
    private const double CashierHomeX = 250;
    private const double CashierHomeY = 320;
    private const double CashierHomeStepX = 30;
    private const double CashierBusyY = 335;
    private const double CookHomeX = 500;
    private const double CookHomeY = 28;
    private const double CookHomeStepX = 40;
    private const double CookBusyX = 460;
    private const double CookBusyY = 28;
    private const double CookBusyStepX = 40;
    private const double ServerHomeX = 955;
    private const double ServerHomeY = 130;
    private const double ServerHomeStepX = 35;
    private const double ServerBusyX = 935;
    private const double ServerBusyY = 240;
    private const double ServerBusyStepX = 30;

    private SimulationEngine? _engine;
    private readonly DispatcherTimer _stateTimer;

    private readonly System.Collections.Generic.List<int> _orderLineIds = new();
    private readonly System.Collections.Generic.List<int> _servingLineIds = new();

    public MainViewModel()
    {
        _stateTimer = new DispatcherTimer { Interval = System.TimeSpan.FromMilliseconds(200) };
        _stateTimer.Tick += (_, _) => RefreshState();

        RebuildStaffList();
    }

    public const double SceneDesignWidth = 1150;
    public const double SceneDesignHeight = 470;

    // обратный масштаб компенсирует растяжение Viewbox для кругов и текста
    public static ScaleTransform SceneInverseScale { get; } = new(1, 1);

    public void OnSceneSizeChanged(double width, double height)
    {
        if (width <= 0 || height <= 0) return;
        SceneInverseScale.ScaleX = SceneDesignWidth / width;
        SceneInverseScale.ScaleY = SceneDesignHeight / height;
    }

    public ObservableCollection<StaffViewModel> StaffList { get; } = new();

    private void RebuildStaffList()
    {
        StaffList.Clear();

        AddStaffRole("Cashier", "Кассир", "🧑‍💼", "#3498DB", ClampCount(CashierCount),
            CashierHomeX, CashierHomeY, CashierHomeStepX, CashierHomeX, CashierBusyY, CashierHomeStepX);
        AddStaffRole("Cook", "Повар", "👨‍🍳", "#E67E22", ClampCount(CookCount),
            CookHomeX, CookHomeY, CookHomeStepX, CookBusyX, CookBusyY, CookBusyStepX);
        AddStaffRole("Server", "Официант", "🙋", "#2ECC71", ClampCount(ServerCount),
            ServerHomeX, ServerHomeY, ServerHomeStepX, ServerBusyX, ServerBusyY, ServerBusyStepX);

        CashiersHeader = Header("Кассир", "Кассиры", ClampCount(CashierCount), "Принимаемый заказ:");
        CooksHeader = Header("Повар", "Повара", ClampCount(CookCount), "Готовится:");
        ServersHeader = Header("Официант", "Официанты", ClampCount(ServerCount), "Доступен к выдаче:");
    }

    private void AddStaffRole(
        string role, string baseLabel, string icon, string color, int count,
        double homeX, double homeY, double homeStepX,
        double busyX, double busyY, double busyStepX)
    {
        for (var i = 0; i < count; i++)
        {
            var offset = i - (count - 1) / 2.0;
            var label = count == 1 ? baseLabel : $"{baseLabel} {i + 1}";

            StaffList.Add(new StaffViewModel(
                role, i, label, icon, color,
                homeX + offset * homeStepX, homeY,
                busyX + offset * busyStepX, busyY));
        }
    }

    private static string Header(string singular, string plural, int count, string tail) =>
        count == 1 ? $"{singular} · {tail}" : $"{plural} ({count}) · {tail}";

    private static int ClampCount(string input) =>
        int.TryParse(input.Trim(), out var value)
            ? Math.Clamp(value, 1, MaxStaffPerRole)
            : 1;

    [ObservableProperty]
    private string _customerArrivalInterval = "1000";

    [ObservableProperty]
    private string _orderTakingInterval = "600";

    [ObservableProperty]
    private string _cookingInterval = "2500";

    [ObservableProperty]
    private string _servingInterval = "800";

    [ObservableProperty]
    private string _cashierCount = "1";

    [ObservableProperty]
    private string _cookCount = "1";

    [ObservableProperty]
    private string _serverCount = "1";

    // аватары пересобираются сразу при вводе
    partial void OnCashierCountChanged(string value) => RebuildStaffList();
    partial void OnCookCountChanged(string value) => RebuildStaffList();
    partial void OnServerCountChanged(string value) => RebuildStaffList();

    [ObservableProperty]
    private string _customerArrivalError = string.Empty;

    [ObservableProperty]
    private string _orderTakingError = string.Empty;

    [ObservableProperty]
    private string _cookingError = string.Empty;

    [ObservableProperty]
    private string _servingError = string.Empty;

    [ObservableProperty]
    private string _cashierCountError = string.Empty;

    [ObservableProperty]
    private string _cookCountError = string.Empty;

    [ObservableProperty]
    private string _serverCountError = string.Empty;

    [ObservableProperty]
    private string _cashiersHeader = "Кассир · Принимаемый заказ:";

    [ObservableProperty]
    private string _cooksHeader = "Повар · Готовится:";

    [ObservableProperty]
    private string _serversHeader = "Официант · Доступен к выдаче:";

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private int _customersWaiting;

    [ObservableProperty]
    private string _takingOrderText = "—";

    [ObservableProperty]
    private string _preparingOrderText = "—";

    [ObservableProperty]
    private string _kitchenWaitingText = "—";

    [ObservableProperty]
    private int _kitchenWaitingCount;

    [ObservableProperty]
    private string _pickupOrderText = "—";

    [ObservableProperty]
    private int _servingLineCustomers;

    public ObservableCollection<CustomerDotViewModel> Customers { get; } = new();
    public ObservableCollection<TicketDotViewModel> Tickets { get; } = new();

    public ObservableCollection<string> LogEntries { get; } = new();

    [RelayCommand]
    private void Start()
    {
        if (IsRunning) return;

        var arrival = IntervalValidator.Validate(CustomerArrivalInterval, "Интервал прихода клиентов");
        var taking = IntervalValidator.Validate(OrderTakingInterval, "Интервал приёма заказа");
        var cooking = IntervalValidator.Validate(CookingInterval, "Интервал приготовления");
        var serving = IntervalValidator.Validate(ServingInterval, "Интервал выдачи заказа");

        var cashiers = IntervalValidator.ValidateCount(CashierCount, "Число кассиров", 1, MaxStaffPerRole);
        var cooks = IntervalValidator.ValidateCount(CookCount, "Число поваров", 1, MaxStaffPerRole);
        var servers = IntervalValidator.ValidateCount(ServerCount, "Число официантов", 1, MaxStaffPerRole);

        CustomerArrivalError = arrival.IsValid ? string.Empty : arrival.ErrorMessage;
        OrderTakingError = taking.IsValid ? string.Empty : taking.ErrorMessage;
        CookingError = cooking.IsValid ? string.Empty : cooking.ErrorMessage;
        ServingError = serving.IsValid ? string.Empty : serving.ErrorMessage;
        CashierCountError = cashiers.IsValid ? string.Empty : cashiers.ErrorMessage;
        CookCountError = cooks.IsValid ? string.Empty : cooks.ErrorMessage;
        ServerCountError = servers.IsValid ? string.Empty : servers.ErrorMessage;

        if (!arrival.IsValid || !taking.IsValid || !cooking.IsValid || !serving.IsValid ||
            !cashiers.IsValid || !cooks.IsValid || !servers.IsValid)
        {
            AppendLog("Запуск отклонён: проверьте введённые интервалы и численность персонала.");
            return;
        }

        var config = new SimulationConfig(
            arrival.Value, taking.Value, cooking.Value, serving.Value,
            cashiers.Value, cooks.Value, servers.Value);
        var engine = new SimulationEngine(config);
        EngineSubscribe(engine);
        _engine = engine;

        RebuildStaffList();
        ResetScene();
        engine.Start();

        IsRunning = true;
        _stateTimer.Start();
    }

    [RelayCommand]
    private async Task StopAsync()
    {
        var engine = _engine;
        if (engine is null || !IsRunning) return;

        _stateTimer.Stop();
        await engine.StopAsync();
        EngineUnsubscribe(engine);
        _engine = null;

        ResetScene();
        RefreshState();
        IsRunning = false;
    }

    private void EngineSubscribe(SimulationEngine engine)
    {
        engine.Log += OnEngineLog;
        engine.CustomerChanged += OnCustomerChanged;
        engine.TicketChanged += OnTicketChanged;
        engine.StaffActivity += OnStaffActivity;
    }

    private void EngineUnsubscribe(SimulationEngine engine)
    {
        engine.Log -= OnEngineLog;
        engine.CustomerChanged -= OnCustomerChanged;
        engine.TicketChanged -= OnTicketChanged;
        engine.StaffActivity -= OnStaffActivity;
    }

    private void OnEngineLog(string message) =>
        Dispatcher.UIThread.Post(() => AppendLog(message));

    private void OnCustomerChanged(CustomerEvent e) =>
        Dispatcher.UIThread.Post(() => ApplyCustomerEvent(e));

    private void OnTicketChanged(string type, int orderNumber) =>
        Dispatcher.UIThread.Post(() => ApplyTicketEvent(type, orderNumber));

    private void OnStaffActivity(string role, int index, bool active) =>
        Dispatcher.UIThread.Post(() => ApplyStaffActivity(role, index, active));

    private void ApplyStaffActivity(string role, int index, bool active)
    {
        var staff = StaffList.FirstOrDefault(s => s.Role == role && s.Index == index);
        if (staff is null) return;

        staff.IsActive = active;
        staff.X = active ? staff.BusyX : staff.HomeX;
        staff.Y = active ? staff.BusyY : staff.HomeY;
    }

    private void ApplyCustomerEvent(CustomerEvent e)
    {
        switch (e.Type)
        {
            case "Arrived":
            {
                var dot = new CustomerDotViewModel(e.CustomerId, e.ColorIndex,
                    SimulationEngine.Palette[e.ColorIndex], SpawnX, WalkY);
                Customers.Add(dot);
                _orderLineIds.Add(e.CustomerId);
                LayoutOrderLine();
                break;
            }
            case "AtCashier":
            {
                _orderLineIds.Remove(e.CustomerId);
                LayoutOrderLine();
                var dot = FindDot(e.CustomerId);
                if (dot is not null)
                {
                    dot.X = CashierX;
                    dot.Y = CashierY;
                    dot.OrderNumber = e.OrderNumber;
                }
                break;
            }
            case "JoinedServingLine":
            {
                _servingLineIds.Add(e.CustomerId);
                LayoutServingLine();
                break;
            }
            case "CalledForPickup":
            {
                var dot = Customers.FirstOrDefault(c => c.OrderNumber == e.OrderNumber);
                if (dot is not null)
                {
                    _servingLineIds.Remove(dot.CustomerId);
                    LayoutServingLine();
                    dot.X = PickupX;
                    dot.Y = PickupY;
                }
                break;
            }
            case "PickedUp":
            {
                var dot = FindDot(e.CustomerId);
                if (dot is not null)
                {
                    dot.X = ExitX;
                    dot.Y = ExitY;
                }
                break;
            }
            case "Left":
            {
                var dot = FindDot(e.CustomerId);
                if (dot is not null) Customers.Remove(dot);
                break;
            }
        }
    }

    private void ApplyTicketEvent(string type, int orderNumber)
    {
        switch (type)
        {
            case "PlacedOnCarousel":
            {
                var ticket = new TicketDotViewModel(orderNumber,
                    KitchenCarouselStartX + Tickets.Count * KitchenCarouselSlot, KitchenCarouselY);
                Tickets.Add(ticket);
                RelayoutKitchenTickets();
                break;
            }
            case "Cooking":
            {
                var ticket = Tickets.FirstOrDefault(t => t.OrderNumber == orderNumber);
                if (ticket is not null)
                {
                    ticket.State = "Cooking";
                    ticket.X = CookingSpotX;
                    ticket.Y = CookingSpotY;
                }
                RelayoutKitchenTickets();
                break;
            }
            case "ServerPickup":
            {
                var ticket = Tickets.FirstOrDefault(t => t.OrderNumber == orderNumber);
                if (ticket is not null)
                {
                    ticket.State = "Carried";
                    ticket.X = ServerHandoverX;
                    ticket.Y = ServerHandoverY;
                }
                break;
            }
            case "OnCounter":
            {
                var ticket = Tickets.FirstOrDefault(t => t.OrderNumber == orderNumber);
                if (ticket is not null)
                {
                    ticket.State = "OnCounter";
                    ticket.X = CounterTicketX;
                    ticket.Y = CounterTicketY;
                }
                break;
            }
            case "PickedUp":
            {
                var ticket = Tickets.FirstOrDefault(t => t.OrderNumber == orderNumber);
                if (ticket is not null) Tickets.Remove(ticket);
                break;
            }
        }
    }

    private CustomerDotViewModel? FindDot(int customerId) =>
        Customers.FirstOrDefault(c => c.CustomerId == customerId);

    // раскладка по рядам: голова очереди ближе к точке обслуживания, при переполнении шаг сжимается
    private void LayoutQueue(IReadOnlyList<int> ids, QueueRow[] rows, double step, double minX)
    {
        var capacity = 0;
        foreach (var row in rows) capacity += row.Count;

        for (var i = 0; i < ids.Count; i++)
        {
            var dot = FindDot(ids[i]);
            if (dot is null) continue;

            if (i < capacity)
            {
                var rest = i;
                foreach (var row in rows)
                {
                    if (rest >= row.Count) { rest -= row.Count; continue; }
                    dot.X = row.HeadX - rest * step;
                    dot.Y = row.Y;
                    break;
                }
            }
            else
            {
                var last = rows[^1];
                var overflow = i - capacity;
                dot.X = Math.Max(minX, last.HeadX - overflow * 10);
                dot.Y = last.Y;
            }
        }
    }

    private void LayoutOrderLine() => LayoutQueue(_orderLineIds, OrderLineRows, OrderLineSlot, OrderLineMinX);

    private void LayoutServingLine() => LayoutQueue(_servingLineIds, ServingLineRows, ServingLineSlot, ServingLineMinX);

    private void RelayoutKitchenTickets()
    {
        var waiting = Tickets.Where(t => t.State == "Waiting").ToList();

        var capacity = 0;
        foreach (var row in TicketRows) capacity += row.Count;

        for (var i = 0; i < waiting.Count; i++)
        {
            var ticket = waiting[i];
            if (i < capacity)
            {
                var rest = i;
                foreach (var row in TicketRows)
                {
                    if (rest >= row.Count) { rest -= row.Count; continue; }
                    ticket.X = row.HeadX + rest * KitchenCarouselSlot;
                    ticket.Y = row.Y;
                    break;
                }
            }
            else
            {
                var last = TicketRows[^1];
                var overflow = i - capacity;
                ticket.X = Math.Min(TicketRowRightLimit - 40, last.HeadX + overflow * 10);
                ticket.Y = last.Y;
            }
        }
    }

    private void RefreshState()
    {
        var engine = _engine;
        if (engine is null) return;

        CustomersWaiting = engine.OrderLine.Count;
        TakingOrderText = FormatOrders(engine.TakingOrders);
        PreparingOrderText = FormatOrders(engine.PreparingOrders);

        var waitingTickets = engine.KitchenCarousel.Snapshot();
        KitchenWaitingCount = waitingTickets.Count;
        KitchenWaitingText = waitingTickets.Count == 0
            ? "—"
            : string.Join(", ", waitingTickets.Select(t => $"№{t.OrderNumber}"));

        PickupOrderText = FormatOrders(engine.AnnouncedOrders);
        ServingLineCustomers = engine.ServingLineCount;
    }

    private static string FormatOrders(IReadOnlyList<int> orders)
    {
        var active = orders.Where(n => n > 0).Select(n => $"№{n}").ToArray();
        return active.Length == 0 ? "—" : string.Join(" · ", active);
    }

    private void AppendLog(string message)
    {
        LogEntries.Add($"[{System.DateTime.Now:HH:mm:ss}] {message}");
        while (LogEntries.Count > 300) LogEntries.RemoveAt(0);
    }

    private void ResetScene()
    {
        Customers.Clear();
        Tickets.Clear();
        _orderLineIds.Clear();
        _servingLineIds.Clear();

        foreach (var staff in StaffList)
        {
            staff.IsActive = false;
            staff.X = staff.HomeX;
            staff.Y = staff.HomeY;
        }
    }
}
