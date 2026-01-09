using System.Collections.Concurrent;
using System.Numerics;

namespace Custom_Async_Await;

/// <summary>
/// Basic example of a custom object pool not the best
/// </summary>
/// <typeparam name="T"></typeparam>
public class CustomArrayPools<T> where T : class, new()
{
    private ConcurrentQueue<T> _queue = new();
    private int _counter = 0;
    public int Available => _queue.Count;
    
    public T Rent()
    {
        
        if (_queue.TryDequeue(out var value))
        {   
            Interlocked.Decrement(ref _counter);
            return value;
        }

        return new T();
    } 

    public void Return(T value)
    {
        _queue.Enqueue(value);
        Interlocked.Increment(ref _counter);
    }
}


/// <summary>
/// Simple stratergy lets bucket array length of 2^x
/// where 2 < x < 30 why this?
/// you won't ask to rent an array to 2^0, 2^1, 2^2, you probaby just hard code that in as it is
/// at 2^30 you hit max length anyway
/// </summary>
/// <typeparam name="T"></typeparam>
public class MyArrayPool<T>
{
    [ThreadStatic] 
    private static T[][] s_tls = new T[30][]; // each thread is also able to have access to one array for each 30 possible lengths
    private readonly ConcurrentQueue<T[]>[] s_arrays = Enumerable.Range(0, 30).Select(_ => new ConcurrentQueue<T[]>()).ToArray();
    public T[] Rent(int minimumLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(minimumLength);
        if (minimumLength == 0)
        {
            return [];
        }

        var index = BitOperations.Log2((uint)minimumLength - 1);
        
        ref T[]? tls = ref s_tls[index];
        //This here makes sure that nearest 2 power length could be returned
        if (tls is not null)
        {
            var temp = tls;
            tls = null;
            return temp;
        }

        //would this make sense to do but you can have a problem here where you might be giving small length over size arrays
        // so lets only search up 2 sizes 
        var maxSearchLength = Math.Min(index + 2, s_tls.Length -1);
        var iter = index;
        while (iter < maxSearchLength)
        {
            ref T[]? nextBest_tls = ref s_tls[iter];
            if (nextBest_tls is null)
            {
                iter++;
                continue;
            }
            
            var temp = nextBest_tls;
            nextBest_tls = null;
            return temp;
        }
        
        var minLengthQueues = s_arrays[index];
        if (minLengthQueues.TryDequeue(out var array))
        {
            return array;
        }
        
        return new T[BitOperations.RoundUpToPowerOf2((uint)minimumLength)];
    }

    public void Return(T[] array)
    {
        ArgumentNullException.ThrowIfNull(array);
        if (array.Length == 0)
        {
            return;
        }

        if (!BitOperations.IsPow2((uint)array.Length))
        {
            throw new InvalidOperationException();
        }
        
        var index = BitOperations.Log2((uint)array.Length - 1);
        
        ref T[]? tls = ref s_tls[index];
        if (tls is null)
        {
            s_tls[index] = array;
            return;
        }
        
        var length = BitOperations.Log2((uint)array.Length - 1);
        var queue = s_arrays[length];
        queue.Enqueue(array);
    }
}


public class UseArrayPoolExample
{
    private MyArrayPool<int> s_arrayPool = new MyArrayPool<int>();

    public int UseArrayPoolExampleForInt(int[] someArray)
    {
        int[]? arrayPoolArray = null;
        var intArray = someArray.Length < 256 // 256 is based on stack size
            ? stackalloc int[someArray.Length] 
            : (arrayPoolArray = s_arrayPool.Rent(someArray.Length));


        if (arrayPoolArray is not null)
        {
            s_arrayPool.Return(arrayPoolArray);
        }

        return 0_0;
    }
    
}