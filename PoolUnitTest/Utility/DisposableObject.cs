namespace PollUnitTest.Utility;

public class DisposableObject : IDisposable
{
	private static int _disposedCount;
	private bool _disposed;

	public static int DisposedCount => _disposedCount;

	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	protected virtual void Dispose(bool disposing)
	{
		if (_disposed)
		{
			return;
		}

		Interlocked.Increment(ref _disposedCount);

		_disposed = true;
	}

	~DisposableObject() => Dispose(false);
}
