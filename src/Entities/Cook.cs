using System;
using System.Threading;
using System.Threading.Tasks;
using FastFoodSimulator.Core;

namespace FastFoodSimulator.Entities;

public sealed class Cook
{
    public event Action<OrderTicket>? CookingStarted;
    public event Action<OrderTicket>? CookingCompleted;

    public async Task RunAsync(
        SimQueue<OrderTicket> kitchenCarousel,
        SimQueue<OrderTicket> serviceQueue,
        SimulationConfig config,
        CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var ticket = await kitchenCarousel.DequeueAsync(ct);
            CookingStarted?.Invoke(ticket);

            await Task.Delay(config.CookingIntervalMs, ct);

            ticket.OrderReadyForServer.TrySetResult();
            serviceQueue.Enqueue(ticket);
            CookingCompleted?.Invoke(ticket);
        }
    }
}
