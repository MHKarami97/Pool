Easy Pool

### Usage

First install package:

> https://www.nuget.org/packages/EasyPool

Then use like these:  

---

simple sample:  

```csharp
using Pool;

var pool = new Pool<object>(() => new object(), null, TimeSpan.FromMinutes(30), initPoolSize: 5, maxPoolSize: 10);

try
{
    var obj = pool.Get();
    pool.Return(obj);
}
finally
{
    pool.Dispose();
}
```

With custom cleanup sample:  

```csharp
using Pool;

Action<object> cleanupAction = obj =>
{
	// Custom cleanup logic (for example, resetting the object state)
	Console.WriteLine("Cleaning up the object...");

	// If the object implements IDisposable, you can dispose it as well (if you pass null for cleanupAction this will be done automatically by default)
	if (obj is IDisposable disposable)
	{
		disposable.Dispose();
		Console.WriteLine("Object disposed.");
	}
};

var pool = new Pool<object>(() => new object(), cleanupAction, TimeSpan.FromMinutes(30), initPoolSize: 5, maxPoolSize: 10);

try
{
    var obj = pool.Get();
    pool.Return(obj);
}
finally
{
    pool.Dispose();
}
```

RabbitMq sample:  

```csharp
using System;
using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Pool;

public class RabbitMqPoolExample
{
    private readonly Pool<IModel> _channelPool;
    private readonly IConnection _connection;
    private const string QueueName = "testQueue";

    public RabbitMqPoolExample()
    {
        // Initialize the single connection
        var factory = new ConnectionFactory() { HostName = "localhost" };
        _connection = factory.CreateConnection();

        // Initialize the pool with a factory to create RabbitMQ channels using the same connection
        _channelPool = new Pool<IModel>(
            factory: () => CreateRabbitMqChannel(_connection),
            cleanupAction: CleanupRabbitMqChannel,
            initPoolSize: 5,
            maxPoolSize: 10
        );

        // Declare queue on all channels when initializing the pool
        for (int i = 0; i < _channelPool.GetCurrentSize(); i++)
        {
            var channel = _channelPool.GetFromPool();
            DeclareQueue(channel);
            _channelPool.ReturnToPool(channel);
        }
    }

    // Factory to create RabbitMQ channels using the single connection
    private IModel CreateRabbitMqChannel(IConnection connection)
    {
        return connection.CreateModel();
    }

    // Cleanup action to close the channel when it's no longer needed
    private void CleanupRabbitMqChannel(IModel channel)
    {
        if (channel != null && channel.IsOpen)
        {
            channel.Close();
            channel.Dispose();
        }
    }

    // Declare the queue on the channel (for publishing and consuming)
    private void DeclareQueue(IModel channel)
    {
        channel.QueueDeclare(queue: QueueName,
                             durable: false,
                             exclusive: false,
                             autoDelete: false,
                             arguments: null);
    }

    // Publish a message to the RabbitMQ queue using pooled channel
    public void PublishMessage(string message)
    {
        var channel = _channelPool.GetFromPool();
        try
        {
            var body = Encoding.UTF8.GetBytes(message);
            channel.BasicPublish(exchange: "",
                                 routingKey: QueueName,
                                 basicProperties: null,
                                 body: body);
            Console.WriteLine($"[x] Sent: {message}");
        }
        finally
        {
            _channelPool.ReturnToPool(channel);
        }
    }

    // Consume messages from the RabbitMQ queue using pooled channel
    public void StartConsuming()
    {
        var channel = _channelPool.GetFromPool();
        try
        {
            var consumer = new EventingBasicConsumer(channel);
            consumer.Received += (model, ea) =>
            {
                var receivedMessage = Encoding.UTF8.GetString(ea.Body.ToArray());
                Console.WriteLine($"[x] Received: {receivedMessage}");
            };

            channel.BasicConsume(queue: QueueName,
                                 autoAck: true,
                                 consumer: consumer);

            Console.WriteLine(" Press [enter] to exit.");
            Console.ReadLine();
        }
        finally
        {
            _channelPool.ReturnToPool(channel);
        }
    }

    public static void Main()
    {
        var example = new RabbitMqPoolExample();

        // Publish a message to the queue
        example.PublishMessage("Hello, RabbitMQ!");

        // Start consuming messages
        example.StartConsuming();
    }
}
```

Sample used configs:  

```csharp
public class Program
{
    public static void Main()
    {
        var pool = new SimpleObjectPool<string>(
            objectFactory: () => "New Object",
            cleanupAction: obj => Console.WriteLine($"Cleaning up {obj}")
        );

        Console.WriteLine($"Max Size: {pool.GetMaxSize()}");

        // Get objects from the pool
        var obj1 = pool.Get();
        Console.WriteLine($"Current Size: {pool.GetCurrentSize()}, Available: {pool.GetAvailableSize()}");

        var obj2 = pool.Get();
        Console.WriteLine($"Current Size: {pool.GetCurrentSize()}, Available: {pool.GetAvailableSize()}");

        // Return objects to the pool
        pool.Return(obj1);
        Console.WriteLine($"Current Size: {pool.GetCurrentSize()}, Available: {pool.GetAvailableSize()}");

        pool.Return(obj2);
        Console.WriteLine($"Current Size: {pool.GetCurrentSize()}, Available: {pool.GetAvailableSize()}");

        // Dispose of the pool and clean up resources
        pool.Dispose();
    }
}
```
