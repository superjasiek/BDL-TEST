using System.Collections.Generic;

namespace BdlGusExporterWPF
{
    public class UserSettings
    {
        public bool UseApiKey { get; set; }
        public string ApiKey { get; set; }
        public List<string> SelectedUnitIds { get; set; }
    }
}
