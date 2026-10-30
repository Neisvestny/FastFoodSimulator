namespace FastFoodSimulator.Core;

public sealed record SimulationConfig(
    int CustomerArrivalIntervalMs,
    int OrderTakingIntervalMs,
    int CookingIntervalMs,
    int ServingIntervalMs,
    int CashierCount = 1,
    int CookCount = 1,
    int ServerCount = 1);
