using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FastFoodSimulator.Core;

public interface ISimQueue
{
    int Count { get; }
    bool TryDequeue();
}

public sealed class SimQueue<T> : ISimQueue
{
    private readonly Queue<T> _items = new();
    private readonly SemaphoreSlim _signal = new(0);
    private readonly object _sync = new();

    public string Name { get; }

    public SimQueue(string name)
    {
        Name = name;
    }

    public void Enqueue(T item)
    {
        lock (_sync)
        {
            _items.Enqueue(item);
        }
        _signal.Release();
    }

    public async Task<T> DequeueAsync(CancellationToken ct)
    {
        await _signal.WaitAsync(ct);
        lock (_sync)
        {
            return _items.Dequeue();
        }
    }

    public int Count
    {
        get { lock (_sync) return _items.Count; }
    }

    public IReadOnlyList<T> Snapshot()
    {
        lock (_sync)
        {
            return _items.ToArray();
        }
    }

    public bool TryDequeue()
    {
        lock (_sync)
        {
            if (_items.Count == 0) return false;
            _items.Dequeue();
            return true;
        }
    }
}
