using Moq;
using PollUnitTest.Utility;
using Pool;

namespace PollUnitTest;

[CollectionDefinition("PoolTests", DisableParallelization = false)]
public class PoolTests
{
	private readonly Mock<Func<object>> _mockFactory;

	public PoolTests()
	{
		_mockFactory = new Mock<Func<object>>();
		_mockFactory.Setup(f => f()).Returns(() => new object());
	}

	[Fact]
	public void Constructor_ShouldInitializePoolWithCorrectSize()
	{
		// Arrange & Act
		var pool = new Pool<object>(_mockFactory.Object, initPoolSize: 10, maxPoolSize: 20);

		// Assert
		for (var i = 0; i < 10; i++)
		{
			Assert.NotNull(pool.GetFromPool());
		}
	}

	[Fact]
	public void Get_ShouldRetrieveItemFromPool()
	{
		// Arrange
		var pool = new Pool<object>(_mockFactory.Object, initPoolSize: 5);

		// Act
		var item = pool.GetFromPool();

		// Assert
		Assert.NotNull(item);
	}

	[Fact]
	public void Get_ShouldCreateNewItemIfPoolIsEmpty()
	{
		// Arrange
		var pool = new Pool<object>(_mockFactory.Object, initPoolSize: 1, maxPoolSize: 10);

		// Act
		var item = pool.GetFromPool();
		var item1 = pool.GetFromPool();

		// Assert
		Assert.NotNull(item);
		Assert.NotNull(item1);
		_mockFactory.Verify(f => f(), Times.Exactly(2));
	}

	[Fact]
	public void Return_ShouldAddItemBackToPool()
	{
		// Arrange
		var pool = new Pool<object>(_mockFactory.Object, initPoolSize: 1);
		var item = pool.GetFromPool();

		// Act
		pool.ReturnToPool(item);
		var returnedItem = pool.GetFromPool();

		// Assert
		Assert.Same(item, returnedItem);
	}

	[Fact]
	public void Return_ShouldThrowIfItemIsNull()
	{
		// Arrange
		var pool = new Pool<object>(_mockFactory.Object);

		// Act & Assert
		Assert.Throws<ArgumentNullException>(() => pool.ReturnToPool(null!));
	}

	[Fact]
	public void Dispose_ShouldDisposeAllItems()
	{
		// Arrange
		var mockDisposable = new Mock<IDisposable>();
		var pool = new Pool<IDisposable>(() => mockDisposable.Object, initPoolSize: 3);

		// Act
		pool.Dispose();

		// Assert
		mockDisposable.Verify(d => d.Dispose(), Times.Exactly(3));
	}

	[Fact]
	public void Dispose_ShouldDisposeSemaphore()
	{
		// Arrange
		var pool = new Pool<object>(_mockFactory.Object);

		// Act
		pool.Dispose();

		// Assert
		Assert.Throws<ObjectDisposedException>(() => pool.GetFromPool());
	}

	[Fact]
	public void Factory_ShouldNotProduceNullItems()
	{
		// Arrange
		_ = new Pool<object>(_mockFactory.Object, initPoolSize: 5);

		// Act & Assert
		Assert.Throws<InvalidOperationException>(() => new Pool<object>(() => null!, null, null, 5, 10));
	}

	[Fact]
	public void ShrinkPool_ShouldReducePoolSize()
	{
		// Arrange
		const int InitPoolSize = 5;
		const int MaxPoolSize = 10;
		var pool = new Pool<object>(() => new object(), null, TimeSpan.FromMinutes(30), InitPoolSize, MaxPoolSize);

		var items = new List<object>();

		for (var i = 0; i < MaxPoolSize; i++)
		{
			items.Add(pool.GetFromPool());
		}

		foreach (var item in items)
		{
			pool.ReturnToPool(item);
		}

		// Assert initial condition
		Assert.Equal(MaxPoolSize, pool.GetCurrentSize());

		// Act
		// Call ShrinkPool directly (simulating timer trigger)
		var shrinkMethod = pool.GetType().GetMethod("ShrinkPool", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
		shrinkMethod?.Invoke(pool, [InitPoolSize]);

		// Assert final condition
		Assert.Equal(InitPoolSize, pool.GetCurrentSize());
	}

	[Fact]
	public void ShrinkPool_ShouldNotReducePoolSize_BelowInitPoolSize()
	{
		// Arrange
		const int InitPoolSize = 5;
		const int MaxPoolSize = 10;
		var pool = new Pool<object>(() => new object(), null, TimeSpan.FromMinutes(30), InitPoolSize, MaxPoolSize);

		// Act
		// Call ShrinkPool directly (simulating timer trigger)
		var shrinkMethod = pool.GetType().GetMethod("ShrinkPool", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
		shrinkMethod?.Invoke(pool, [InitPoolSize]);

		// Assert that pool size is not below initial size
		Assert.Equal(InitPoolSize, pool.GetCurrentSize());
	}

	[Fact]
	public void CleanupAction_ShouldBeInvoked_WhenShrink()
	{
		// Arrange
		const int InitPoolSize = 5;
		const int MaxPoolSize = 10;
		var mockItem = new Mock<IModel>();
		var factoryMock = new Mock<Func<IModel>>();

		factoryMock.Setup(f => f()).Returns(mockItem.Object);

		var pool = new Pool<IModel>(
			factoryMock.Object,
			CleanupAction,
			TimeSpan.FromMinutes(30),
			InitPoolSize,
			MaxPoolSize
		);

		// Act
		var items = new List<IModel>();

		for (var i = 0; i < MaxPoolSize; i++)
		{
			items.Add(pool.GetFromPool());
		}

		foreach (var i in items)
		{
			pool.ReturnToPool(i);
		}

		var shrinkMethod = pool.GetType().GetMethod("ShrinkPool", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
		shrinkMethod?.Invoke(pool, [InitPoolSize]);

		// Assert
		mockItem.Verify(m => m.Close(), Times.Exactly(InitPoolSize));

		return;

		// Action that will invoke Close method on IModel
		void CleanupAction(IModel item)
		{
			item.Close();
		}
	}

	[Fact]
	public void CleanupAction_ShouldBeInvoked_WhenPoolIsDisposed()
	{
		// Arrange
		const int InitPoolSize = 5;
		const int MaxPoolSize = 10;
		var mockItem = new Mock<IModel>();
		var factoryMock = new Mock<Func<IModel>>();

		factoryMock.Setup(f => f()).Returns(mockItem.Object);

		var pool = new Pool<IModel>(
			factoryMock.Object,
			CleanupAction,
			TimeSpan.FromMinutes(30),
			InitPoolSize,
			MaxPoolSize
		);

		// Act
		var items = new List<IModel>();

		for (var i = 0; i < MaxPoolSize; i++)
		{
			items.Add(pool.GetFromPool());
		}

		foreach (var i in items)
		{
			pool.ReturnToPool(i);
		}

		pool.Dispose();

		// Assert
		mockItem.Verify(m => m.Close(), Times.Exactly(MaxPoolSize));

		return;

		// Action that will invoke Close method on IModel
		void CleanupAction(IModel item)
		{
			item.Close();
		}
	}
}
