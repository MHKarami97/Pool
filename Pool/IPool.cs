namespace Pool;

public interface IPool<T> : IDisposable where T : class
{
    T Get();
    void Return(T channel);
}