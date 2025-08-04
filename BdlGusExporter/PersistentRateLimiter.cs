using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace BdlGusExporterWPF
{
    public class PersistentRateLimiter
    {
        private readonly string _persistencePath;
        private Dictionary<string, RateLimiterState> _apiStates = new();

        private readonly Dictionary<string, (TimeSpan, int)> _anonymousLimits;
        private readonly Dictionary<string, (TimeSpan, int)> _registeredLimits;

        private const string AnonymousUserKey = "__anonymous__";

        public PersistentRateLimiter(string persistencePath = "rate_limiter_state.json")
        {
            _persistencePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, persistencePath);

            _anonymousLimits = new Dictionary<string, (TimeSpan, int)>
            {
                { "15m", (TimeSpan.FromMinutes(15), 100) },
                { "12h", (TimeSpan.FromHours(12), 1000) },
                { "7d", (TimeSpan.FromDays(7), 10000) }
            };

            _registeredLimits = new Dictionary<string, (TimeSpan, int)>
            {
                { "15m", (TimeSpan.FromMinutes(15), 500) },
                { "12h", (TimeSpan.FromHours(12), 5000) },
                { "7d", (TimeSpan.FromDays(7), 50000) }
            };
        }

        public void LoadState()
        {
            try
            {
                if (File.Exists(_persistencePath))
                {
                    var json = File.ReadAllText(_persistencePath);
                    _apiStates = JsonSerializer.Deserialize<Dictionary<string, RateLimiterState>>(json) ?? new();
                }
            }
            catch (Exception)
            {
                // Handle exceptions (e.g., corrupted file) by starting with a fresh state
                _apiStates = new();
            }
        }

        public void SaveState()
        {
            try
            {
                var json = JsonSerializer.Serialize(_apiStates, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_persistencePath, json);
            }
            catch (Exception)
            {
                // Handle exceptions during save
            }
        }

        public bool TryAcquire(string apiKey)
        {
            var userKey = string.IsNullOrWhiteSpace(apiKey) ? AnonymousUserKey : apiKey;
            var limits = string.IsNullOrWhiteSpace(apiKey) ? _anonymousLimits : _registeredLimits;

            if (!_apiStates.ContainsKey(userKey))
            {
                _apiStates[userKey] = new RateLimiterState();
                foreach (var period in limits.Keys)
                {
                    _apiStates[userKey].RequestTimestamps[period] = new List<DateTime>();
                }
            }

            var state = _apiStates[userKey];
            var now = DateTime.UtcNow;

            // First, check if all limits are met
            foreach (var (period, (window, limit)) in limits)
            {
                if (!state.RequestTimestamps.ContainsKey(period))
                {
                    state.RequestTimestamps[period] = new List<DateTime>();
                }

                state.RequestTimestamps[period].RemoveAll(ts => now - ts > window);

                if (state.RequestTimestamps[period].Count >= limit)
                {
                    return false;
                }
            }

            // If all checks pass, add the new timestamp
            foreach (var period in limits.Keys)
            {
                state.RequestTimestamps[period].Add(now);
            }

            return true;
        }

        public string GetStatistics(string apiKey)
        {
            var userKey = string.IsNullOrWhiteSpace(apiKey) ? AnonymousUserKey : apiKey;
            var limits = string.IsNullOrWhiteSpace(apiKey) ? _anonymousLimits : _registeredLimits;

            if (!_apiStates.ContainsKey(userKey))
            {
                _apiStates[userKey] = new RateLimiterState();
                 foreach (var period in limits.Keys)
                {
                    _apiStates[userKey].RequestTimestamps[period] = new List<DateTime>();
                }
            }

            var state = _apiStates[userKey];
            var now = DateTime.UtcNow;
            var statsBuilder = new System.Text.StringBuilder();
            statsBuilder.Append("Dostępne limity: ");

            foreach (var (period, (window, limit)) in limits)
            {
                if (!state.RequestTimestamps.ContainsKey(period))
                {
                    state.RequestTimestamps[period] = new List<DateTime>();
                }

                state.RequestTimestamps[period].RemoveAll(ts => now - ts > window);
                var currentCount = state.RequestTimestamps[period].Count;
                statsBuilder.Append($"| {period}: {limit - currentCount}/{limit} ");
            }
            return statsBuilder.ToString().TrimEnd();
        }
    }
}
