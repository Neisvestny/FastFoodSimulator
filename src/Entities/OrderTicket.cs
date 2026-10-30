using System.Threading.Tasks;

namespace FastFoodSimulator.Entities;

public sealed class OrderTicket
{
    public int OrderNumber { get; }

    public OrderTicket(int orderNumber)
    {
        OrderNumber = orderNumber;
    }

    // сервер ждёт, пока повар не готовит
    public TaskCompletionSource OrderReadyForServer { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    // клиент ждёт, пока не вызовут номер
    public TaskCompletionSource OrderReadyForCustomer { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public override string ToString() => $"Заказ №{OrderNumber}";
}
