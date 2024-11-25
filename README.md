Easy Pool

### Usage

First install package:

> https://www.nuget.org/packages/EasyPool

```csharp
using Pool;

var pool = new Pool<object>(() => new object(), initPoolSize: 5, maxPoolSize: 10);

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
