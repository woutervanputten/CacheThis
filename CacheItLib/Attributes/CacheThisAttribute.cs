using Microsoft.Extensions.Caching.Memory;
using System;

namespace CacheThisLib
{
    [AttributeUsage(AttributeTargets.Method, Inherited = true)]
    public class CacheThisAttribute : Attribute
    {
        // Expose cache duration in seconds for simplicity in attribute usage
        public double AbsoluteExpirationRelativeToNow { get; set; } = -1;  // Default of -1 means not set

        public double SlidingExpirationInSeconds { get; set; } = -1;  // Default of -1 means not set

        public string HashingMethod { get; set; } = "SHA256";

        [Obsolete]
        public CacheItemPriority Priority { get; set; } = CacheItemPriority.Normal;

        // Convert seconds to TimeSpan if needed internally when applying the caching strategy
        public TimeSpan? GetAbsoluteExpirationRelativeToNow()
        {
            if (AbsoluteExpirationRelativeToNow >= 0)
                return TimeSpan.FromSeconds(AbsoluteExpirationRelativeToNow);
            return null;
        }

        public TimeSpan? GetSlidingExpiration()
        {
            if (SlidingExpirationInSeconds >= 0)
                return TimeSpan.FromSeconds(SlidingExpirationInSeconds);
            return null;
        }
    }
}



