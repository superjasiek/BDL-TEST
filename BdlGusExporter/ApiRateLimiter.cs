using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.RateLimiting;

namespace BdlGusExporterWPF
{
    public class ApiRateLimiter : IDisposable
    {
        private readonly PersistentRateLimiter _persistentRateLimiter;

        // 1-second limiter remains in-memory only as it's not critical to persist
        private readonly SlidingWindowRateLimiter _anonymousSecondLimiter = new(new SlidingWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromSeconds(1), SegmentsPerWindow = 1, AutoReplenishment = true });
        private readonly SlidingWindowRateLimiter _registeredSecondLimiter = new(new SlidingWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromSeconds(1), SegmentsPerWindow = 1, AutoReplenishment = true });


        public ApiRateLimiter()
        {
            _persistentRateLimiter = new PersistentRateLimiter();
            _persistentRateLimiter.LoadState();
        }

        public async Task<RateLimitLease> AcquireAsync(string apiKey, CancellationToken cancellationToken = default)
        {
            bool isRegistered = !string.IsNullOrWhiteSpace(apiKey);
            var secondLimiter = isRegistered ? _registeredSecondLimiter : _anonymousSecondLimiter;

            // First, acquire the 1-second in-memory lease
            var secondLease = await secondLimiter.AcquireAsync(1, cancellationToken);
            if (!secondLease.IsAcquired)
            {
                // This shouldn't happen with AcquireAsync unless cancelled, but as a safeguard:
                return secondLease;
            }

            // Then, check the persistent limiters
            if (_persistentRateLimiter.TryAcquire(apiKey))
            {
                // If successful, return a composite lease that holds the 1s lease
                return new PersistentLease(secondLease, true);
            }
            else
            {
                // If persistent check fails, dispose the 1s lease and return a failed lease
                secondLease.Dispose();
                return new PersistentLease(null, false);
            }
        }

        public string GetStatistics(string apiKey)
        {
            return _persistentRateLimiter.GetStatistics(apiKey);
        }

        public void SaveState()
        {
            _persistentRateLimiter.SaveState();
        }

        public void Dispose()
        {
            SaveState();
            _anonymousSecondLimiter.Dispose();
            _registeredSecondLimiter.Dispose();
        }

        // A custom lease class to represent the outcome of our combined limiting logic
        private class PersistentLease : RateLimitLease
        {
            private readonly RateLimitLease _innerLease; // Can be null if not acquired
            public override bool IsAcquired { get; }

            public PersistentLease(RateLimitLease innerLease, bool acquired)
            {
                _innerLease = innerLease;
                IsAcquired = acquired;
            }

            public override IEnumerable<string> MetadataNames => _innerLease?.MetadataNames ?? System.Linq.Enumerable.Empty<string>();

            public override bool TryGetMetadata(string metadataName, out object metadata)
            {
                if (_innerLease != null)
                {
                    return _innerLease.TryGetMetadata(metadataName, out metadata);
                }
                metadata = null;
                return false;
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    _innerLease?.Dispose();
                }
            }
        }
    }
}
