namespace Clave.OctoLocal
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Microsoft.Extensions.CommandLineUtils;
    using Octopus.Client.Model;
    using Octostache;

    public static class Static
    {
        public static IReadOnlyCollection<string> FindOctopusFiles(string currentDirectory, List<string> configFilePattern)
        {
            return FileSystem.FindFiles(currentDirectory, configFilePattern.ToArray()).ToList();
        }

        public static Func<string, string> MakeAbsolutePath(string currentDirectory)
        {
            return f => Path.IsPathRooted(f) ? f : Path.Combine(currentDirectory, f);
        }

        public static VariableDictionary CreateVariables(
            IReadOnlyDictionary<string, string> variableSetVariables,
            IReadOnlyDictionary<string, string> projectVariables,
            LocalConfig localConfig,
            IReadOnlyDictionary<string, string> userConfig,
            IReadOnlyDictionary<string, string> sessionVariables)
        {
            return new VariableDictionary()
                .CopyFrom(localConfig.Variables)
                .CopyFrom(variableSetVariables)
                .CopyFrom(projectVariables)
                .CopyFrom(localConfig.OverrideVariables)
                .CopyFrom(userConfig)
                .CopyFrom(sessionVariables);
        }

        public static IReadOnlyDictionary<string, string> GetSessionVariables(IReadOnlyCollection<string> variables)
        {
            return variables.Select(v => v.Split('=')).ToDictionary(l => l[0], l => string.Join('=', l.Skip(1)));
        }

        public static VariableDictionary CopyFrom(this VariableDictionary to, IReadOnlyDictionary<string, string> from)
        {
            if (from == null)
            {
                return to;
            }

            foreach (var pair in from)
            {
                to[pair.Key] = pair.Value;
            }

            return to;
        }

        public static IReadOnlyDictionary<string, string> ToDictionary(this IList<VariableResource> variables)
        {
            var result = new Dictionary<string, string>(variables.Count);
            foreach (var variable in variables)
            {
                result[variable.Name] = variable.Value;
            }

            return result;
        }

        public static List<string> ValuesOrNull(this CommandOption option) => option.HasValue() ? option.Values : null;

        public static List<string> ValuesOrEmpty(this CommandOption option) => option.HasValue() ? option.Values : new List<string>(0);
    }
}