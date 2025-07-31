using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.RateLimiting;

namespace BdlGusExporterWPF
{
    public class ApiRateLimiter : IDisposable
    {
        private readonly Dictionary<string, SlidingWindowRateLimiter> _anonymousLimiters;
        private readonly Dictionary<string, SlidingWindowRateLimiter> _registeredLimiters;

        private readonly Dictionary<string, SlidingWindowRateLimiterOptions> _anonymousOptions;
        private readonly Dictionary<string, SlidingWindowRateLimiterOptions> _registeredOptions;

        public ApiRateLimiter()
        {
            _anonymousOptions = new Dictionary<string, SlidingWindowRateLimiterOptions>
            {
                { "1s", new SlidingWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromSeconds(1), SegmentsPerWindow = 1, AutoReplenishment = true } },
                { "15m", new SlidingWindowRateLimiterOptions { PermitLimit = 100, Window = TimeSpan.FromMinutes(15), SegmentsPerWindow = 1, AutoReplenishment = true } },
                { "12h", new SlidingWindowRateLimiterOptions { PermitLimit = 1000, Window = TimeSpan.FromHours(12), SegmentsPerWindow = 1, AutoReplenishment = true } },
                { "7d", new SlidingWindowRateLimiterOptions { PermitLimit = 10000, Window = TimeSpan.FromDays(7), SegmentsPerWindow = 1, AutoReplenishment = true } }
            };
            _anonymousLimiters = _anonymousOptions.ToDictionary(kvp => kvp.Key, kvp => new SlidingWindowRateLimiter(kvp.Value));

            _registeredOptions = new Dictionary<string, SlidingWindowRateLimiterOptions>
            {
                { "1s", new SlidingWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromSeconds(1), SegmentsPerWindow = 1, AutoReplenishment = true } },
                { "15m", new SlidingWindowRateLimiterOptions { PermitLimit = 500, Window = TimeSpan.FromMinutes(15), SegmentsPerWindow = 1, AutoReplenishment = true } },
                { "12h", new SlidingWindowRateLimiterOptions { PermitLimit = 5000, Window = TimeSpan.FromHours(12), SegmentsPerWindow = 1, AutoReplenishment = true } },
                { "7d", new SlidingWindowRateLimiterOptions { PermitLimit = 50000, Window = TimeSpan.FromDays(7), SegmentsPerWindow = 1, AutoReplenishment = true } }
            };
            _registeredLimiters = _registeredOptions.ToDictionary(kvp => kvp.Key, kvp => new SlidingWindowRateLimiter(kvp.Value));
        }

        public async Task<RateLimitLease> AcquireAsync(bool isRegistered, CancellationToken cancellationToken = default)
        {
            var limiters = isRegistered ? _registeredLimiters.Values.ToList() : _anonymousLimiters.Values.ToList();
            var leases = new List<RateLimitLease>();
            try
            {
                foreach (var limiter in limiters)
                {
                    var lease = await limiter.AcquireAsync(1, cancellationToken);
                    leases.Add(lease);
                }
                return new CompositeLease(leases);
            }
            catch
            {
                foreach (var lease in leases)
                {
                    lease.Dispose();
                }
                throw;
            }
        }

        public string GetStatistics(bool isRegistered)
        {
            var limiters = isRegistered ? _registeredLimiters : _anonymousLimiters;
            var options = isRegistered ? _registeredOptions : _anonymousOptions;
            var statsBuilder = new StringBuilder();
            statsBuilder.Append("Dostępne limity: ");

            foreach (var (period, limiter) in limiters)
            {
                var stats = limiter.GetStatistics();
                var permitLimit = options[period].PermitLimit;
                if (stats != null)
                {
                    statsBuilder.Append($"| {period}: {stats.CurrentAvailablePermits}/{permitLimit} ");
                }
            }
            return statsBuilder.ToString().TrimEnd();
        }

        public void Dispose()
        {
            foreach (var limiter in _anonymousLimiters.Values)
            {
                limiter.Dispose();
            }
            foreach (var limiter in _registeredLimiters.Values)
            {
                limiter.Dispose();
            }
        }

        private class CompositeLease : RateLimitLease
        {
            private readonly List<RateLimitLease> _leases;

            public CompositeLease(List<RateLimitLease> leases)
            {
                _leases = leases;
            }

            public override bool IsAcquired => true;

            public override IEnumerable<string> MetadataNames => _leases.SelectMany(l => l.MetadataNames).Distinct();

            public override bool TryGetMetadata(string metadataName, out object metadata)
            {
                foreach (var lease in _leases)
                {
                    if (lease.TryGetMetadata(metadataName, out metadata))
                    {
                        return true;
                    }
                }
                metadata = null;
                return false;
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    foreach (var lease in _leases.AsEnumerable().Reverse())
                    {
                        lease.Dispose();
                    }
                }
            }
        }
    }
}
