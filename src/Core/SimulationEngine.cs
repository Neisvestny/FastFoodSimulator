using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FastFoodSimulator.Entities;

namespace FastFoodSimulator.Core;

public sealed record CustomerEvent(string Type, int CustomerId, int ColorIndex, int OrderNumber);

public sealed class SimulationEngine
{
    private readonly SimulationConfig _config;
    private readonly CancellationTokenSource _cts = new();
    private readonly List<Task> _processes = new();
    private readonly object _sync = new();
    private readonly List<Customer> _servingLine = new();

    private int _customerIdSeq;
    private int _colorIndexSeq;

    public SimulationEngine(SimulationConfig config)
    {
        _config = config;
        _currentTakingOrders = new int[config.CashierCount];
        _currentPreparingOrders = new int[config.CookCount];
        _lastAnnouncedOrders = new int[config.ServerCount];
    }

    public SimQueue<Customer> OrderLine { get; } = new("Линия заказов");
    public SimQueue<OrderTicket> KitchenCarousel { get; } = new("Карусель кухни");
    public SimQueue<OrderTicket> ServiceQueue { get; } = new("Очередь выдачи");

    public event Action<string>? Log;
    public event Action<CustomerEvent>? CustomerChanged;
    public event Action<string, int>? TicketChanged;
    public event Action<string, int, bool>? StaffActivity;

    public bool IsRunning { get; private set; }

    private readonly int[] _currentTakingOrders;
    private readonly int[] _currentPreparingOrders;
    private readonly int[] _lastAnnouncedOrders;

    public IReadOnlyList<int> TakingOrders => Snapshot(_currentTakingOrders);
    public IReadOnlyList<int> PreparingOrders => Snapshot(_currentPreparingOrders);
    public IReadOnlyList<int> AnnouncedOrders => Snapshot(_lastAnnouncedOrders);

    private static IReadOnlyList<int> Snapshot(int[] source)
    {
        var copy = new int[source.Length];
        Array.Copy(source, copy, source.Length);
        return copy;
    }

    public int ServingLineCount { get { lock (_sync) return _servingLine.Count; } }

    public void Start()
    {
        if (IsRunning) return;
        IsRunning = true;

        var numbers = new OrderNumberGenerator();

        for (var i = 0; i < _config.CashierCount; i++)
        {
            var index = i;
            var cashier = new OrderTaker();
            WireCashierEvents(cashier, index);
            _processes.Add(cashier.RunAsync(OrderLine, KitchenCarousel, _config, numbers, _cts.Token));
        }

        for (var i = 0; i < _config.CookCount; i++)
        {
            var index = i;
            var cook = new Cook();
            WireCookEvents(cook, index);
            _processes.Add(cook.RunAsync(KitchenCarousel, ServiceQueue, _config, _cts.Token));
        }

        for (var i = 0; i < _config.ServerCount; i++)
        {
            var index = i;
            var server = new Server();
            WireServerEvents(server, index);
            _processes.Add(server.RunAsync(ServiceQueue, _config, _cts.Token));
        }

        _processes.Add(CustomerArrivalProcessAsync());

        Log?.Invoke(
            $"Симуляция запущена. Кассиры: {_config.CashierCount} · " +
            $"Повара: {_config.CookCount} · Официанты: {_config.ServerCount}.");
    }

    public async Task StopAsync()
    {
        if (!IsRunning) return;
        IsRunning = false;

        _cts.Cancel();
        try
        {
            await Task.WhenAll(_processes.ToArray());
        }
        catch (OperationCanceledException)
        {
        }
        _processes.Clear();

        ClearQueues();
        ResetState();

        Log?.Invoke("Симуляция остановлена. Состояние полностью сброшено.");
    }

    private static string StaffName(string baseName, int index, int total) =>
        total <= 1 ? baseName : $"{baseName} #{index + 1}";

    private void WireCashierEvents(OrderTaker cashier, int index)
    {
        var who = StaffName("Кассир", index, _config.CashierCount);

        cashier.CustomerAtCashier += c =>
        {
            Log?.Invoke($"{who} обслуживает клиента #{c.Id}...");
            StaffActivity?.Invoke("Cashier", index, true);
        };

        cashier.OrderTaken += info =>
        {
            Volatile.Write(ref _currentTakingOrders[index], info.Ticket.OrderNumber);
            Log?.Invoke($"Заказ №{info.Ticket.OrderNumber} оформлен для клиента #{info.Customer.Id}. Чек выдан.");
            CustomerChanged?.Invoke(new CustomerEvent("AtCashier", info.Customer.Id, info.Customer.ColorIndex, info.Ticket.OrderNumber));

            lock (_sync) _servingLine.Add(info.Customer);
            CustomerChanged?.Invoke(new CustomerEvent("JoinedServingLine", info.Customer.Id, info.Customer.ColorIndex, info.Ticket.OrderNumber));
            Log?.Invoke($"Клиент #{info.Customer.Id} встал в очередь на выдачу (ждут: {ServingLineCount}).");

            _ = CustomerWaitProcessAsync(info.Customer, info.Ticket);
        };

        cashier.TicketPlacedOnCarousel += t =>
        {
            Log?.Invoke($"Тикет №{t.OrderNumber} помещён на карусель кухни.");
            TicketChanged?.Invoke("PlacedOnCarousel", t.OrderNumber);
            Volatile.Write(ref _currentTakingOrders[index], 0);
            StaffActivity?.Invoke("Cashier", index, false);
        };
    }

    private void WireCookEvents(Cook cook, int index)
    {
        var who = StaffName("Повар", index, _config.CookCount);

        cook.CookingStarted += t =>
        {
            Volatile.Write(ref _currentPreparingOrders[index], t.OrderNumber);
            Log?.Invoke($"{who} снял тикет №{t.OrderNumber} с карусели и начал готовить...");
            TicketChanged?.Invoke("Cooking", t.OrderNumber);
            StaffActivity?.Invoke("Cook", index, true);
        };

        cook.CookingCompleted += t =>
        {
            Log?.Invoke($"Заказ №{t.OrderNumber} готов. {who} передал его официанту.");
            StaffActivity?.Invoke("Cook", index, false);
        };
    }

    private void WireServerEvents(Server server, int index)
    {
        var who = StaffName("Официант", index, _config.ServerCount);

        server.ServerTookOrder += t =>
        {
            Log?.Invoke($"{who} забрал заказ №{t.OrderNumber} у повара.");
            TicketChanged?.Invoke("ServerPickup", t.OrderNumber);
            StaffActivity?.Invoke("Server", index, true);
        };

        server.OrderAnnounced += t =>
        {
            Volatile.Write(ref _lastAnnouncedOrders[index], t.OrderNumber);
            Log?.Invoke($"{who} вызывает номер {t.OrderNumber}! Заказ стоит на стойке выдачи.");
            CustomerChanged?.Invoke(new CustomerEvent("CalledForPickup", 0, 0, t.OrderNumber));
            TicketChanged?.Invoke("OnCounter", t.OrderNumber);
            StaffActivity?.Invoke("Server", index, false);
        };
    }

    private async Task CustomerWaitProcessAsync(Customer customer, OrderTicket ticket)
    {
        var ct = _cts.Token;
        try
        {
            await ticket.OrderReadyForCustomer.Task.WaitAsync(ct);

            lock (_sync) _servingLine.Remove(customer);
            CustomerChanged?.Invoke(new CustomerEvent("PickedUp", customer.Id, customer.ColorIndex, ticket.OrderNumber));
            TicketChanged?.Invoke("PickedUp", ticket.OrderNumber);
            Log?.Invoke($"Клиент #{customer.Id} забрал заказ №{ticket.OrderNumber} и уходит в зону питания.");

            await Task.Delay(1200, ct);
            CustomerChanged?.Invoke(new CustomerEvent("Left", customer.Id, customer.ColorIndex, ticket.OrderNumber));
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task CustomerArrivalProcessAsync()
    {
        var ct = _cts.Token;
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(_config.CustomerArrivalIntervalMs, ct);

                var customer = new Customer(
                    Interlocked.Increment(ref _customerIdSeq),
                    Interlocked.Increment(ref _colorIndexSeq) % Palette.Count);

                OrderLine.Enqueue(customer);
                Log?.Invoke($"Клиент #{customer.Id} пришёл и встал в линию заказов (ждут: {OrderLine.Count}).");
                CustomerChanged?.Invoke(new CustomerEvent("Arrived", customer.Id, customer.ColorIndex, 0));
            }
        }
        catch (OperationCanceledException)
        {
            Log?.Invoke("Процесс появления клиентов остановлен.");
        }
    }

    private void ClearQueues()
    {
        foreach (var queue in new ISimQueue[] { OrderLine, KitchenCarousel, ServiceQueue })
        {
            while (queue.Count > 0) queue.TryDequeue();
        }
        lock (_sync) _servingLine.Clear();
    }

    private void ResetState()
    {
        Array.Clear(_currentTakingOrders, 0, _currentTakingOrders.Length);
        Array.Clear(_currentPreparingOrders, 0, _currentPreparingOrders.Length);
        Array.Clear(_lastAnnouncedOrders, 0, _lastAnnouncedOrders.Length);
    }

    public static IReadOnlyList<string> Palette { get; } = new[]
    {
        "#E74C3C", "#3498DB", "#2ECC71", "#F39C12", "#9B59B6",
        "#1ABC9C", "#E67E22", "#34495E", "#E84393", "#00CEC9",
        "#FDCB6E", "#6C5CE7"
    };
}
