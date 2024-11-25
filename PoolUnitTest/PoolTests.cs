using Moq;
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
		var pool = new Pool<object>(_mockFactory.Object, initPoolSize: 5);

		// Act & Assert
		Assert.Throws<InvalidOperationException>(() => new Pool<object>(() => null!, 5, 10));
	}
}
