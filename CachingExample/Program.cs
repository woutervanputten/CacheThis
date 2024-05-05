using System;
using System.Reflection;
using System.Collections.Concurrent;
using System.Text;
using System.Security.Cryptography;
using CacheThisLib;
using Microsoft.Extensions.Caching.Memory;

namespace Caching;

// Define the attribute to be used for caching.
[AttributeUsage(AttributeTargets.Method)]
public class CacheAttribute : Attribute
{
    private static readonly ConcurrentDictionary<string, object?> _cache = new ConcurrentDictionary<string, object?>();



    /// <summary>
    /// Generates a secure cache key based on the method and its arguments using SHA-256, optimized for performance and memory usage.
    /// </summary>
    /// <param name="method">The method for which the cache key is being generated.</param>
    /// <param name="args">The arguments to the method, if any.</param>
    /// <returns>A string representing the unique and secure cache key.</returns>
    private string GenerateCacheKey(MethodBase method, object?[]? args)
    {
        if (method == null)
        {
            throw new ArgumentNullException(nameof(method));
        }

        var declaringType = method.DeclaringType;
        if (declaringType == null)
        {
            throw new InvalidOperationException("Method must have a declaring type.");
        }

        using var sha256 = SHA256.Create();
        using var stream = new MemoryStream();

        // Use StreamWriter for efficient byte conversions with buffered writing
        using (var writer = new StreamWriter(stream, Encoding.UTF8, 1024, leaveOpen: true))
        {
            writer.Write(declaringType.FullName);
            writer.Write('.');
            writer.Write(method.Name);

            foreach (object? arg in args ?? Array.Empty<object?>())
            {
                writer.Write(';');
                writer.Write(arg?.ToString() ?? string.Empty);
            }
        }

        stream.Position = 0; // Reset position to read the stream from the beginning
        var hashBytes = sha256.ComputeHash(stream);

        // Convert byte array to a hexadecimal string efficiently
        return ByteArrayToHexViaStringBuilder(hashBytes);
    }

    /// <summary>
    /// Converts a byte array to a hexadecimal string using a StringBuilder for efficiency.
    /// </summary>
    /// <param name="bytes">Byte array to convert.</param>
    /// <returns>Hexadecimal string representation of the byte array.</returns>
    private static string ByteArrayToHexViaStringBuilder(byte[] bytes)
    {
        var stringBuilder = new StringBuilder(bytes.Length * 2);
        foreach (byte b in bytes)
        {
            stringBuilder.Append(b.ToString("x2"));
        }
        return stringBuilder.ToString();
    }

    public object? GetCachedValue(MethodBase method, object?[]? args)
    {
        string key = GenerateCacheKey(method, args);
        if (_cache.TryGetValue(key, out object? value))
        {
            return value;
        }
        return null;
    }

    public void SetCacheValue(MethodBase method, object?[]? args, object? result)
    {
        string key = GenerateCacheKey(method, args);
        _cache.TryAdd(key, result);
    }
}

// Proxy class for handling caching.
public class CacheProxy<T> : DispatchProxy
{
    private T _decorated;

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod == null)
            throw new ArgumentNullException(nameof(targetMethod));

        var cacheAttr = targetMethod.GetCustomAttribute<CacheAttribute>();

        if (cacheAttr != null)
        {
            var cachedValue = cacheAttr.GetCachedValue(targetMethod, args);
            if (cachedValue != null)
            {
                return cachedValue;
            }
        }

        var result = targetMethod.Invoke(_decorated, args);
        cacheAttr?.SetCacheValue(targetMethod, args, result);

        return result;
    }

    public void SetDecorated(T decorated)
    {
        _decorated = decorated;
    }
}

// Cache interceptor to create proxy instances.
public static class CacheInterceptor
{
    public static TInterface CreateWithTarget<TInterface, TConcrete>(Func<TConcrete> factoryMethod)
        where TConcrete : class, TInterface
    {
        var proxy = DispatchProxy.Create<TInterface, CacheProxy<TInterface>>() as CacheProxy<TInterface>;
        var instance = factoryMethod();
        proxy?.SetDecorated(instance);
        return (TInterface)(object)proxy;
    }
}

// Interface and its implementation.
public interface IPlus
{
    [Cache]
    long Compute(int a, int b);
}


public class Plus : IPlus
{
    private IPlus _self;

    public Plus()
    {
        _self = CacheInterceptor.CreateWithTarget<IPlus, PlusInternal>(() => new PlusInternal());
    }

    public long Compute(int a, int b)
    {
        return _self.Compute(a, b);
    }

    private class PlusInternal : IPlus
    {
        public long Compute(int a, int b)
        {
            Thread.Sleep(2000);
            return a + b; // Perform the actual computation here
        }
    }
}


public class Minus
{
    [CacheThis(AbsoluteExpirationRelativeToNow=0.3, SlidingExpirationInSeconds =0.1)]
    public  virtual long Compute(int a, int b)
    {
        return (long)a - b;
    }

    [CacheThis]
    public  virtual int TitaTest(int a, int b)
    {
        Thread.Sleep(5000);
        return a + b;
    }

    [CacheThis(AbsoluteExpirationRelativeToNow = 300)]
    public  virtual int[] GenerateFibonacciSequence(int numberOfTerms)
    {
        if (numberOfTerms <= 0)
        {
            throw new ArgumentException("Number of terms must be positive.");
        }

        int[] sequence = new int[numberOfTerms];
        sequence[0] = 0;
        sequence[1] = 1;

        for (int i = 2; i < numberOfTerms; i++)
        {
            sequence[i] = sequence[i - 1] + sequence[i - 2];
        }

        return sequence;
    }
    
    [CacheThis(AbsoluteExpirationRelativeToNow = 300)]
    public virtual int[] FibRecursive(int numberOfTerms)
    {
        if (numberOfTerms <= 0)
        {
            throw new ArgumentException("Number of terms must be positive.");
        }

        // Base cases: directly returning the initial elements of the Fibonacci sequence
        if (numberOfTerms == 1)
            return new int[] { 0 };
        if (numberOfTerms == 2)
            return new int[] { 0, 1 };

        // Recursively build the Fibonacci sequence
        int[] sequence = FibRecursive(numberOfTerms - 1);
        Array.Resize(ref sequence, numberOfTerms);
        sequence[numberOfTerms - 1] = sequence[numberOfTerms - 2] + sequence[numberOfTerms - 3];
        return sequence;
    }

}

class Program
{
    static void Main(string[] args)
    {

        Console.WriteLine("Start.");
        var minus = new Minus();
        var minusCached = new Minus_Cached(minus);
        var result = minusCached.TitaTest(2, 1);

        Console.WriteLine($"Computed Result: {result}");
        result = minusCached.TitaTest(2, 1);
        Console.WriteLine($"Computed Result: {result}");  // Outputs the result with cachingg
        
        testc(minusCached);
        testc(minusCached);


        var entOpt = new MemoryCacheEntryOptions();

    }

    static void testc(Minus minusObj)
    {
        Console.WriteLine(minusObj.TitaTest(2,1));
    }
}
