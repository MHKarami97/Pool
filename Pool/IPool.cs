namespace Pool;

/// <summary>
/// Easy Pool
/// </summary>
/// <typeparam name="T">The type of objects to be pooled.</typeparam>
public interface IPool<T> : IDisposable where T : class
{
	/// <summary>
	/// Retrieves an item from the pool.
	/// </summary>
	/// <returns>An item from the pool.</returns>
	/// <exception cref="InvalidOperationException">Thrown when the pool fails to create a new resource.</exception>
	T GetFromPool();

	/// <summary>
	/// Returns an item back to the pool.
	/// </summary>
	/// <param name="item">The item to return to the pool.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="item"/> is null.</exception>
	void ReturnToPool(T item);
}
