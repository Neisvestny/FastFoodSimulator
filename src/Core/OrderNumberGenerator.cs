using System.Threading;

namespace FastFoodSimulator.Core;

public sealed class OrderNumberGenerator
{
    private int _last;

    public int Next() => Interlocked.Increment(ref _last);
}
