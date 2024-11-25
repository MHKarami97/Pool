using System.Collections.Concurrent;

namespace Pool;

/// <summary>
/// Easy Pool
/// </summary>
/// <typeparam name="T">The type of objects to be pooled.</typeparam>
public class Pool<T> : IPool<T> where T : class
{
	private readonly SemaphoreSlim _semaphore;
	private readonly ConcurrentBag<T> _items;
	private readonly Func<T> _factory;
	private readonly int _maxPoolSize;
	private int _currentSize;
	private bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="Pool{T}"/> class.
	/// </summary>
	/// <param name="factory">A function to create new instances of <typeparamref name="T"/>.</param>
	/// <param name="initPoolSize">The initial number of objects to be created and added to the pool. Default is 100.</param>
	/// <param name="maxPoolSize">The maximum number of objects that can be in the pool. Default is <see cref="int.MaxValue"/>.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="factory"/> is null.</exception>
	/// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="initPoolSize"/> is negative or <paramref name="maxPoolSize"/> is less than or equal to zero.</exception>
	/// <exception cref="ArgumentException">Thrown when <paramref name="maxPoolSize"/> is less than <paramref name="initPoolSize"/>.</exception>
	public Pool(Func<T> factory, int initPoolSize = 100, int maxPoolSize = int.MaxValue)
	{
#if NET8_0
		ArgumentNullException.ThrowIfNull(factory);
		ArgumentOutOfRangeException.ThrowIfNegative(initPoolSize);
#else
		if (factory is null)
		{
			throw new ArgumentNullException(nameof(factory));
		}

		if (initPoolSize < 1)
		{
			throw new ArgumentNullException(nameof(initPoolSize));
		}
#endif

		if (maxPoolSize <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(maxPoolSize), Resources.Max_pool_Size_Min_Value);
		}

		if (maxPoolSize < initPoolSize)
		{
			throw new ArgumentException(Resources.Max_Pool_Size_More_Than_Init);
		}

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

	/// <summary>
	/// Retrieves an item from the pool.
	/// </summary>
	/// <returns>An item from the pool.</returns>
	/// <exception cref="InvalidOperationException">Thrown when the pool fails to create a new resource.</exception>
	public T GetFromPool()
	{
		_semaphore.Wait();

		try
		{
			var item = _items.TryTake(out var result) ? result : TryCreate();

			if (item == null)
			{
				_ = _semaphore.Release();
				throw new InvalidOperationException(Resources.Failed_Create_Resource);
			}

			return item;
		}
		catch
		{
			_ = _semaphore.Release();
			throw;
		}
	}

	/// <summary>
	/// Returns an item back to the pool.
	/// </summary>
	/// <param name="item">The item to return to the pool.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="item"/> is null.</exception>
	public void ReturnToPool(T item)
	{
#if NET8_0
		ArgumentNullException.ThrowIfNull(item);
#else
		if (item is null)
		{
			throw new ArgumentNullException(nameof(item));
		}
#endif

		_items.Add(item);
		_semaphore.Release();
	}

	private T Create()
	{
		try
		{
			var item = _factory();

			return item ?? throw new InvalidOperationException(Resources.Factory_Produced_Null_Item);
		}
		catch (Exception ex)
		{
			throw new InvalidOperationException(Resources.Erro_Creation, ex);
		}
	}

	private T TryCreate()
	{
		var newSize = Interlocked.Increment(ref _currentSize);

		if (newSize > _maxPoolSize)
		{
			_ = Interlocked.Decrement(ref _currentSize);
			throw new InvalidOperationException(Resources.Poo_Maximum_Capacity);
		}

		try
		{
			return Create();
		}
		catch
		{
			_ = Interlocked.Decrement(ref _currentSize);
			throw;
		}
	}

	/// <summary>
	/// Disposes the pool and releases all resources.
	/// </summary>
	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	/// <summary>
	/// Disposes the pool and releases all resources.
	/// </summary>
	/// <param name="disposing">A boolean value indicating whether the method is called from the Dispose method.</param>
	protected virtual void Dispose(bool disposing)
	{
		if (_disposed)
		{
			return;
		}

		if (disposing)
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

		_disposed = true;
	}
}
