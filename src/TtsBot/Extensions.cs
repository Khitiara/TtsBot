using System.Buffers;

namespace TtsBot;

public static class Extensions
{
    public static ArrayPoolLease<T> Lease<T>(this ArrayPool<T> pool, int sizeHint, out T[] array) {
        ArgumentNullException.ThrowIfNull(pool);
        return new ArrayPoolLease<T>(pool, array = pool.Rent(sizeHint));
    }
}

public readonly ref struct ArrayPoolLease<T>(ArrayPool<T> pool, T[] array)
{
    private readonly ArrayPool<T> _pool  = pool;
    private readonly T[]          _array = array;

    public void Dispose() {
        _pool.Return(_array);
    }
}