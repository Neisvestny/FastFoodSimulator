using System;
using System.Threading;
using System.Threading.Tasks;
using FastFoodSimulator.Core;

namespace FastFoodSimulator.Entities;

public sealed record OrderTakenInfo(Customer Customer, OrderTicket Ticket);

public sealed class OrderTaker
{
    public event Action<Customer>? CustomerAtCashier;
    public event Action<OrderTakenInfo>? OrderTaken;
    public event Action<OrderTicket>? TicketPlacedOnCarousel;

    public async Task RunAsync(
        SimQueue<Customer> orderLine,
        SimQueue<OrderTicket> kitchenCarousel,
        SimulationConfig config,
        OrderNumberGenerator numbers,
        CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var customer = await orderLine.DequeueAsync(ct);
            CustomerAtCashier?.Invoke(customer);

            await Task.Delay(config.OrderTakingIntervalMs, ct);

            var ticket = new OrderTicket(numbers.Next());
            customer.CurrentOrderNumber = ticket.OrderNumber;
            OrderTaken?.Invoke(new OrderTakenInfo(customer, ticket));

            kitchenCarousel.Enqueue(ticket);
            TicketPlacedOnCarousel?.Invoke(ticket);
        }
    }
}
