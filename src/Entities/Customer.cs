namespace FastFoodSimulator.Entities;

public enum CustomerState
{
    WaitingInOrderLine,
    AtCashier,
    InServingLine,
    AtPickup,
    Leaving
}

public sealed class Customer
{
    public int Id { get; }
    public int ColorIndex { get; }

    // 0 — заказ ещё не оформлен
    public int CurrentOrderNumber { get; set; }

    public Customer(int id, int colorIndex)
    {
        Id = id;
        ColorIndex = colorIndex;
    }

    public override string ToString() => $"Клиент #{Id}";
}
