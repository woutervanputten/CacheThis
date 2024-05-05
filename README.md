# CacheThis
## Overview
CacheThis is a library designed to facilitate method caching within classes. It operates through a source generator that detects the presence of the `[CacheThis]` attribute.

Simply annotate the methods you wish to cache with the `[CacheThis]` attribute. Subsequently, the source generator will generate code to produce a derived class named `<ClassThatContainsTheMethod>_Cached`.

This generated class retains the original functionality of the defined class while incorporating caching capabilities.

## Example

```csharp
public class Minus
{
    [CacheThis(AbsoluteExpirationRelativeToNow=0.3, SlidingExpirationInSeconds =0.1)]
    public virtual long Compute(int a, int b)
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
        Console.WriteLine($"Computed Result: {result}");  // Outputs the result with caching
        
        testc(minusCached); // shows the Minus_Cached object can be used a Minus object
    }

    static void testc(Minus minusObj)
    {
        Console.WriteLine(minusObj.Compute(2,1));
    }
}
```

## The [CacheThis] attribute
Adding the [CacheThis] attribute will signal to the source generator that the method should be cached.

### [CacheThis] attribute Parameters
The following parameters are supported currently

* AbsoluteExpirationRelativeToNow
* SlidingExpirationInSeconds
* To be added: Priority
* To be added: Size

