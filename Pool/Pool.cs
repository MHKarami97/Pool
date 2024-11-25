using System.Collections.Concurrent;

namespace Pool;

public class Pool<T> : IPool<T> where T : class
{
    private readonly SemaphoreSlim _semaphore;
    private readonly ConcurrentBag<T> _items;
    private readonly Func<T> _factory;
    private readonly int _maxPoolSize;
    private int _currentSize;

    public Pool(Func<T> factory, int initPoolSize = 100, int maxPoolSize = int.MaxValue)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentOutOfRangeException.ThrowIfNegative(initPoolSize);

        if (maxPoolSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxPoolSize), "Max pool size must be greater than zero.");

        if (maxPoolSize < initPoolSize)
            throw new ArgumentException("Maximum pool size must be greater than or equal to the initial pool size.");

        _items = [];
        _factory = factory;
        _maxPoolSize = maxPoolSize;
        _currentSize = initPoolSize;
        _semaphore = new SemaphoreSlim(initPoolSize, maxPoolSize);

        for (var i = 0; i < initPoolSize; i++)
        {
            _items.Add(Create());
        }
    }

    public T Get()
    {
        _semaphore.Wait();

        try
        {
            var item = _items.TryTake(out var result) ? result : TryCreate();

            if (item == null)
            {
                _semaphore.Release();
                throw new InvalidOperationException("Failed to create a new resource.");
            }

            return item;
        }
        catch
        {
            _semaphore.Release();
            throw;
        }
    }

    public void Return(T item)
    {
        ArgumentNullException.ThrowIfNull(item);

        _items.Add(item);
        _semaphore.Release();
    }

    private T Create()
    {
        try
        {
            var item = _factory();

            return item ?? throw new InvalidOperationException("Factory produced a null item");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Error occurred during resource creation.", ex);
        }
    }

    private T TryCreate()
    {
        var newSize = Interlocked.Increment(ref _currentSize);

        if (newSize > _maxPoolSize)
        {
            Interlocked.Decrement(ref _currentSize);
            throw new InvalidOperationException("Pool size exceeded maximum capacity.");
        }

        try
        {
            return Create();
        }
        catch
        {
            Interlocked.Decrement(ref _currentSize);
            throw;
        }
    }

    public void Dispose()
    {
        _semaphore.Dispose();

        while (_items.TryTake(out var item))
        {
            if (item is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}