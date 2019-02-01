namespace Clave.OctoLocal
{
    using System.Collections.Generic;

    public class LocalConfig
    {
        public IReadOnlyCollection<string> Projects { get; set; }

        public string ProjectId { get; set; }

        public Dictionary<string, string> Variables { get; set; }

        public Dictionary<string, string> OverrideVariables { get; set; }

        public List<string> Exclude { get; set; }

        public List<string> ConfigFilePattern { get; set; } = new List<string> { "*.octopus.config => *.config", "*.octopus.json => *.json" };
    }
}