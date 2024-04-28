# Cache Cow
Cache Cow is a library that enables the caching of methods of classes.

Example:

public class Minus
{
    [CacheIt(AbsoluteExpirationRelativeToNow=0.3, SlidingExpirationInSeconds =0.1)]
    public  virtual long Compute(int a, int b)
    {
        return (long)a - b;
    }
}

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Start.");
        var minus = new Minus();
        var minusCached = new Minus_Cached(minus);
        var result = minusCached.Compute(2, 1);

        Console.WriteLine($"Computed Result: {result}");
        result = minusCached.Compute(2, 1);
        Console.WriteLine($"Computed Result: {result}");  // Outputs the result with cachingg
        
        testc(minusCached);
        testc(minusCached);
        testc(minusCached);
        testc(minusCached);
        testc(minusCached);
        testc(minusCached);
        testc(minusCached);
        testc(minusCached);
        testc(minusCached);
        testc(minusCached);
        testc(minusCached);
        testc(minusCached);
        testc(minusCached);

    }

    static void testc(Minus minusObj)
    {
        Console.WriteLine(minusObj.Compute(2,1));
    }
}
