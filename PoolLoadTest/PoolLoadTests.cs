using System.Security.Cryptography;
using Pool;
using Xunit.Abstractions;

namespace PoolLoadTest;

public class PoolLoadTests(ITestOutputHelper testOutputHelper)
{
	[Fact]
	public async Task Pool_Should_Handle_Concurrent_Access()
	{
		// Arrange
		const int NumThreads = 100;
		const int PoolSize = 50;
		const int MaxPoolSize = 200;

		var pool = new Pool<string>(
			() => Guid.NewGuid().ToString(),
			cleanupAction: _ =>
			{
				// cleanup action no-op for strings
			},
			initPoolSize: PoolSize,
			maxPoolSize: MaxPoolSize);

		var tasks = new List<Task>();

		// Act: Start multiple tasks to simulate concurrent usage of the pool
		for (var i = 0; i < NumThreads; i++)
		{
			tasks.Add(Task.Run(() =>
			{
				// Simulate getting and returning items to/from the pool
				var item = pool.GetFromPool();
				testOutputHelper.WriteLine($"Thread {Environment.CurrentManagedThreadId} got item: {item}");

				// Simulate some work with the item
				Thread.Sleep(GetSecureRandomInt(1, 100)); // Random delay to simulate work

				pool.ReturnToPool(item);
				testOutputHelper.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId} returned item.");
			}));
		}

		// Wait for all tasks to complete
		await Task.WhenAll(tasks).ConfigureAwait(true);

		// Assert: After the test, check pool size and available items
		Assert.InRange(pool.GetCurrentSize(), PoolSize, MaxPoolSize);
		Assert.Equal(pool.GetMaxSize() - pool.GetCurrentSize(), pool.GetAvailableSize());
		testOutputHelper.WriteLine($"Pool size after test: {pool.GetCurrentSize()}");
		testOutputHelper.WriteLine($"Available pool slots: {pool.GetAvailableSize()}");
	}

	[Fact]
	public async Task Pool_Should_Throw_Exception_When_Exceed_Max_Pool_Size()
	{
		// Arrange
		const int PoolSize = 10;
		const int MaxPoolSize = 10;

		var pool = new Pool<string>(
			() => Guid.NewGuid().ToString(),
			cleanupAction: _ =>
			{
				// cleanup action no-op for strings
			},
			initPoolSize: PoolSize,
			maxPoolSize: MaxPoolSize);

		var tasks = new List<Task>();

		// Act: Try to get more items than the pool's max size
		for (var i = 0; i < MaxPoolSize + 1; i++)
		{
			tasks.Add(Task.Run(() =>
			{
				// Simulate getting an item from the pool
				try
				{
					var item = pool.GetFromPool();
					Thread.Sleep(GetSecureRandomInt(1, 100)); // Random delay to simulate work
					pool.ReturnToPool(item);
				}
				catch (Exception ex)
				{
					// Expected: Exception when exceeding max pool size
					Assert.True(ex is InvalidOperationException, "Expected InvalidOperationException");
				}
			}));
		}

		// Wait for all tasks to complete
		await Task.WhenAll(tasks).ConfigureAwait(true);
	}

	private int GetSecureRandomInt(int minValue, int maxValue)
	{
		using var rng = RandomNumberGenerator.Create();
		var randomBytes = new byte[4]; // 4 bytes for an int
		rng.GetBytes(randomBytes);
		var randomValue = BitConverter.ToInt32(randomBytes, 0);

		return Math.Abs(randomValue % (maxValue - minValue)) + minValue;
	}
}
