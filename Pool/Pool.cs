using System.Collections.Concurrent;

namespace Pool;

/// <summary>
/// Easy Pool
/// </summary>
/// <typeparam name="T">The type of objects to be pooled.</typeparam>
public class Pool<T> : IPool<T> where T : class
{
	private readonly TimeSpan _defaultShrinkInterval = TimeSpan.FromMinutes(30);
	private TaskCompletionSource<bool>? _shrinkCompletionSource;
	private readonly System.Timers.Timer _shrinkTimer;
	private readonly Action<T> _cleanupAction;
	private readonly SemaphoreSlim _semaphore;
	private readonly ConcurrentBag<T> _items;
	private readonly object _lock = new();
	private readonly Func<T> _factory;
	private readonly int _createIncrement;
	private readonly int _maxPoolSize;
	private bool _isShrinking;
	private int _currentSize;
	private bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="Pool{T}"/> class.
	/// </summary>
	/// <param name="factory">A function to create new instances of <typeparamref name="T"/>.</param>
	/// <param name="cleanupAction">Call on Shrink and Dispose to clean <typeparamref name="T"/>. default is call .Dispose if item is Disposable</param>
	/// <param name="shrinkInterval">Time interval to shrink unused pools and disposed them, then reset to initPoolSize, default value is 30 min on null param</param>
	/// <param name="initPoolSize">The initial number of objects to be created and added to the pool. Default is 100.</param>
	/// <param name="maxPoolSize">The maximum number of objects that can be in the pool. Default is <see cref="int.MaxValue"/>.</param>
	/// <param name="createIncrement">When there is no object on pool, how many new item should create</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="factory"/> is null.</exception>
	/// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="initPoolSize"/> is negative or <paramref name="maxPoolSize"/> is less than or equal to zero.</exception>
	/// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="shrinkInterval"/> is less than 30 min (just if not null)</exception>
	/// <exception cref="ArgumentException">Thrown when <paramref name="maxPoolSize"/> is less than <paramref name="initPoolSize"/>.</exception>
	public Pool(Func<T> factory, Action<T>? cleanupAction = null, TimeSpan? shrinkInterval = null, int initPoolSize = 100, int maxPoolSize = int.MaxValue, int createIncrement = 1)
	{
#if NET8_0
		ArgumentNullException.ThrowIfNull(factory);
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(initPoolSize);
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(createIncrement);
#else
		if (factory is null)
		{
			throw new ArgumentNullException(nameof(factory), Resources.Object_Can_Not_Be_Null);
		}

		if (initPoolSize < 1)
		{
			throw new ArgumentOutOfRangeException(nameof(initPoolSize), Resources.Can_Not_Be_Zero);
		}

		if (createIncrement < 1)
		{
			throw new ArgumentOutOfRangeException(nameof(createIncrement), Resources.Can_Not_Be_Zero);
		}
#endif

		if (maxPoolSize <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(maxPoolSize), Resources.Max_pool_Size_Min_Value);
		}

		if (maxPoolSize < initPoolSize)
		{
			throw new ArgumentOutOfRangeException(nameof(maxPoolSize), Resources.Max_Pool_Size_More_Than_Init);
		}

		if (maxPoolSize - initPoolSize < createIncrement)
		{
			throw new ArgumentOutOfRangeException(nameof(createIncrement), Resources.Max_Create_Increament);
		}

		if (shrinkInterval != null && shrinkInterval < TimeSpan.FromMinutes(30))
		{
			throw new ArgumentOutOfRangeException(nameof(shrinkInterval), Resources.Min_Shrink_Interval);
		}

		_items = [];
		_factory = factory;
		_maxPoolSize = maxPoolSize;
		_currentSize = initPoolSize;
		_createIncrement = createIncrement;
		_semaphore = new SemaphoreSlim(maxPoolSize, maxPoolSize);
		_shrinkCompletionSource = new TaskCompletionSource<bool>();
		_cleanupAction = cleanupAction ?? (item =>
		{
			if (item is IDisposable disposable)
			{
				disposable.Dispose();
			}
		});

		for (var i = 0; i < initPoolSize; i++)
		{
			Create();
		}

#if NET8_0
		_shrinkTimer = new System.Timers.Timer(shrinkInterval ?? _defaultShrinkInterval) { AutoReset = true, Enabled = true };
		_shrinkTimer.Elapsed += async (_, _) => await Task.Run(() => ShrinkPool(initPoolSize)).ConfigureAwait(false);
#else
		var interval = shrinkInterval ?? _defaultShrinkInterval;
		_shrinkTimer = new System.Timers.Timer(interval.TotalMilliseconds) { AutoReset = true, Enabled = true };
		_shrinkTimer.Elapsed += async (_, _) => await Task.Run(() => ShrinkPool(initPoolSize)).ConfigureAwait(false);
#endif
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
			// Avoid taking items if the pool is shrinking
			WaitForShrinkToCompleteAsync().Wait();

			_ = _items.TryTake(out var result);

			if (result == null)
			{
				lock (_lock)
				{
					//Maybe another thread create item
					_ = _items.TryTake(out result);

					if (result == null)
					{
						TryCreate(_createIncrement);
						_ = _items.TryTake(out result);
					}
				}
			}

			if (result == null)
			{
				_ = _semaphore.Release();
				throw new InvalidOperationException(Resources.Failed_Create_Resource);
			}

			return result;
		}
		catch
		{
			_ = _semaphore.Release();
			throw;
		}
	}

	/// <summary>
	/// Retrieves an item from the pool.
	/// </summary>
	/// <returns>An item from the pool.</returns>
	/// <exception cref="InvalidOperationException">Thrown when the pool fails to create a new resource.</exception>
	public async Task<T> GetFromPoolAsync()
	{
		await _semaphore.WaitAsync().ConfigureAwait(false);

		try
		{
			// Avoid taking items if the pool is shrinking
			await WaitForShrinkToCompleteAsync().ConfigureAwait(false);

			_ = _items.TryTake(out var result);

			if (result == null)
			{
				lock (_lock)
				{
					//Maybe another thread create item
					_ = _items.TryTake(out result);

					if (result == null)
					{
						TryCreate(_createIncrement);
						_ = _items.TryTake(out result);
					}
				}
			}

			if (result == null)
			{
				_ = _semaphore.Release();
				throw new InvalidOperationException(Resources.Failed_Create_Resource);
			}

			return result;
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

	/// <summary>
	/// Get the number of remaining threads that can enter
	/// </summary>
	/// <returns></returns>
	public int GetCurrentCount() => _semaphore.CurrentCount;

	/// <summary>
	/// Get the current size of the pool.
	/// </summary>
	/// <returns></returns>
	public int GetCurrentSize() => _currentSize;

	/// <summary>
	/// Get the maximum size of the pool.
	/// </summary>
	/// <returns></returns>
	public int GetMaxSize() => _maxPoolSize;

	/// <summary>
	/// Get the available size of the pool.
	/// </summary>
	/// <returns></returns>
	public int GetAvailableSize() => _maxPoolSize - _currentSize;

	/// <summary>
	/// Stop Channel pool and Dispose all items
	/// </summary>
	public void Stop() => Dispose();

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
			_shrinkTimer.Dispose();

			while (_items.TryTake(out var item))
			{
				_cleanupAction(item);
			}
		}

		_disposed = true;
	}

	private void TryCreate(int count)
	{
		for (var i = 0; i < count; i++)
		{
			try
			{
				TryCreateSingle();
			}
			catch (InvalidOperationException)
			{
			}
		}
	}

	private void TryCreateSingle()
	{
		var newSize = Interlocked.Increment(ref _currentSize);

		if (newSize > _maxPoolSize)
		{
			_ = Interlocked.Decrement(ref _currentSize);
			throw new InvalidOperationException(Resources.Poo_Maximum_Capacity);
		}

		try
		{
			Create();
		}
		catch
		{
			_ = Interlocked.Decrement(ref _currentSize);
			throw;
		}
	}

	private void Create()
	{
		try
		{
			var item = _factory();

			if (item != null)
			{
				_items.Add(item);
				return;
			}

			throw new InvalidOperationException(Resources.Factory_Produced_Null_Item);
		}
		catch (InvalidOperationException)
		{
			throw;
		}
		catch (Exception ex)
		{
			throw new InvalidOperationException(Resources.Erro_Creation, ex);
		}
	}

	private async Task WaitForShrinkToCompleteAsync()
	{
		if (_isShrinking)
		{
			// If shrink is in progress, we need to wait until the TaskCompletionSource is set
			await (_shrinkCompletionSource?.Task ?? Task.CompletedTask).ConfigureAwait(false);
		}
	}

	private void ShrinkPool(int initPoolSize)
	{
		lock (_lock)
		{
			if (_disposed)
			{
				return;
			}

			try
			{
				_isShrinking = true;
				_ = _shrinkCompletionSource?.TrySetResult(true);

				var itemsToRemove = Math.Max(0, _currentSize - initPoolSize);

				for (var i = 0; i < itemsToRemove; i++)
				{
					try
					{
						if (_items.TryTake(out var item))
						{
							_cleanupAction(item);

							_ = Interlocked.Decrement(ref _currentSize);
						}
						else
						{
							break;
						}
					}
					catch (Exception e)
					{
						Console.WriteLine(e);
					}
				}
			}
			finally
			{
				_isShrinking = false;

				// After shrinking is complete, reset the TaskCompletionSource for the next time
				_shrinkCompletionSource = new TaskCompletionSource<bool>();
			}
		}
	}
}
