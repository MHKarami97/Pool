using Moq;
using Pool;

namespace PollUnitTest
{
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
                Assert.NotNull(pool.Get());
            }
        }

        [Fact]
        public void Get_ShouldRetrieveItemFromPool()
        {
            // Arrange
            var pool = new Pool<object>(_mockFactory.Object, initPoolSize: 5);

            // Act
            var item = pool.Get();

            // Assert
            Assert.NotNull(item);
        }

        [Fact]
        public void Get_ShouldCreateNewItemIfPoolIsEmpty()
        {
            // Arrange
            var pool = new Pool<object>(_mockFactory.Object, initPoolSize: 0);

            // Act
            var item = pool.Get();

            // Assert
            Assert.NotNull(item);
            _mockFactory.Verify(f => f(), Times.Once);
        }

        [Fact]
        public void Return_ShouldAddItemBackToPool()
        {
            // Arrange
            var pool = new Pool<object>(_mockFactory.Object, initPoolSize: 1);
            var item = pool.Get();

            // Act
            pool.Return(item);
            var returnedItem = pool.Get();

            // Assert
            Assert.Same(item, returnedItem);
        }

        [Fact]
        public void Return_ShouldThrowIfItemIsNull()
        {
            // Arrange
            var pool = new Pool<object>(_mockFactory.Object);

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => pool.Return(null));
        }

        [Fact]
        public void Get_ShouldThrowIfExceedingMaxPoolSize()
        {
            // Arrange
            var pool = new Pool<object>(_mockFactory.Object, initPoolSize: 1, maxPoolSize: 1);
            pool.Get();

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => pool.Get());
        }

        [Fact]
        public async Task ConcurrentAccess_ShouldHandleMultipleThreads()
        {
            // Arrange
            var pool = new Pool<object>(_mockFactory.Object, initPoolSize: 5, maxPoolSize: 10);
            const int threads = 50;

            // Act
            var tasks = new Task<object>[threads];
            for (var i = 0; i < threads; i++)
            {
                tasks[i] = Task.Run(() => pool.Get());
            }

            var results = await Task.WhenAll(tasks);

            // Assert
            Assert.Equal(threads, results.Length);
            foreach (var result in results)
            {
                Assert.NotNull(result);
            }
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
            Assert.Throws<ObjectDisposedException>(() => pool.Get());
        }

        [Fact]
        public void Factory_ShouldNotProduceNullItems()
        {
            // Arrange
            var pool = new Pool<object>(_mockFactory.Object, initPoolSize: 5);

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => new Pool<object>(() => null, 5, 10));
        }
    }
}