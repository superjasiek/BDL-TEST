using System;
using System.Collections.Generic;
using System.Threading.RateLimiting;

namespace BdlGusExporterWPF
{
    public sealed class RateLimiterService : IDisposable
    {
        public List<RateLimiter> AnonymousLimiters { get; }
        public List<RateLimiter> RegisteredLimiters { get; }

        public RateLimiterService()
        {
            AnonymousLimiters = CreateAnonymousLimiters();
            RegisteredLimiters = CreateRegisteredLimiters();
        }

        public List<RateLimiter> GetLimiters(bool isRegistered)
        {
            return isRegistered ? RegisteredLimiters : AnonymousLimiters;
        }

        private static List<RateLimiter> CreateAnonymousLimiters()
        {
            return new List<RateLimiter>
            {
                new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromSeconds(1), AutoReplenishment = true }),
                new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions { PermitLimit = 100, Window = TimeSpan.FromMinutes(15), AutoReplenishment = true }),
                new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions { PermitLimit = 1000, Window = TimeSpan.FromHours(12), AutoReplenishment = true }),
                new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions { PermitLimit = 10000, Window = TimeSpan.FromDays(7), AutoReplenishment = true })
            };
        }

        private static List<RateLimiter> CreateRegisteredLimiters()
        {
            return new List<RateLimiter>
            {
                new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromSeconds(1), AutoReplenishment = true }),
                new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions { PermitLimit = 500, Window = TimeSpan.FromMinutes(15), AutoReplenishment = true }),
                new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions { PermitLimit = 5000, Window = TimeSpan.FromHours(12), AutoReplenishment = true }),
                new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions { PermitLimit = 50000, Window = TimeSpan.FromDays(7), AutoReplenishment = true })
            };
        }

        public void Dispose()
        {
            foreach (var limiter in AnonymousLimiters)
            {
                limiter.Dispose();
            }
            foreach (var limiter in RegisteredLimiters)
            {
                limiter.Dispose();
            }
        }
    }
}
