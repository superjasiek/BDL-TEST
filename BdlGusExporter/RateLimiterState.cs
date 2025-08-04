using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace BdlGusExporterWPF
{
    public class RateLimiterState
    {
        [JsonInclude]
        public Dictionary<string, List<DateTime>> RequestTimestamps { get; internal set; } = new();

        public RateLimiterState()
        {
            // Required for JSON deserialization
        }
    }
}
