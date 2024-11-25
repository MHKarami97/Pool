using NBomber.CSharp;
using Pool;

namespace PoolLoadTest;

public class PoolLoadTests
{
	[Fact]
	public void LoadTest_By()
	{
		var pool = new Pool<object>(() => new object(), initPoolSize: 10, maxPoolSize: 50);

		var scenario = Scenario.Create("pool_stress_test", async context =>
			{
				try
				{
					var item = pool.GetFromPool();
					await Task.Delay(10).ConfigureAwait(true); // Simulate some work with the item
					pool.ReturnToPool(item);

					return Response.Ok();
				}
				catch (Exception ex)
				{
					return Response.Fail(message: ex.Message);
				}
			})
			.WithLoadSimulations(
				Simulation.Inject(rate: 100, interval: TimeSpan.FromSeconds(30), during: TimeSpan.FromSeconds(30))
			);

		NBomberRunner
			.RegisterScenarios(scenario)
			.Run();
	}
}
