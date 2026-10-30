using System;
using System.Threading;
using System.Threading.Tasks;
using FastFoodSimulator.Core;

namespace FastFoodSimulator.Entities;

public sealed class Server
{
    public event Action<OrderTicket>? ServerTookOrder;
    public event Action<OrderTicket>? OrderAnnounced;

    public async Task RunAsync(
        SimQueue<OrderTicket> serviceQueue,
        SimulationConfig config,
        CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var ticket = await serviceQueue.DequeueAsync(ct);
            ServerTookOrder?.Invoke(ticket);
            await ticket.OrderReadyForServer.Task.WaitAsync(ct);

            await Task.Delay(config.ServingIntervalMs, ct);

            ticket.OrderReadyForCustomer.TrySetResult();
            OrderAnnounced?.Invoke(ticket);
        }
    }
}
