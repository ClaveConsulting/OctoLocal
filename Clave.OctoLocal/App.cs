namespace Clave.OctoLocal
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using Octopus.Client;
    using Octopus.Client.Model;
    using Octostache;

    public class App
    {
        private readonly AnsiConsole _console;
        private readonly bool _quiet;

        public App(AnsiConsole console, bool quiet)
        {
            _console = console;
            _quiet = quiet;
        }

        public async Task<int> Run(IReadOnlyCollection<string> files, IReadOnlyCollection<string> variables)
        {
            var globalConfig = await GetGlobalConfig();
            var currentDirectory = FileSystem.GetCurrentDirectory();
            var endpoint = new OctopusServerEndpoint(globalConfig.Server, globalConfig.ApiKey);
            using (var client = await OctopusAsyncClient.Create(endpoint))
            {
                await RecursivelySubstitute(files, client, currentDirectory, Static.GetSessionVariables(variables), globalConfig);
            }

            DebugLine(() => "Done");
            return 0;
        }

        public async Task<int> Init(IReadOnlyCollection<string> projects)
        {
            if (!projects.Any())
            {
                WriteLine(Colors.Red("Specify projects\ndotnet octolocal init -p path\\to\\project -p path\\to\\second\\project"));
                return -1;
            }

            var currentDirectory = FileSystem.GetCurrentDirectory();
            var path = Path.Combine(currentDirectory, "octolocal.json");
            var localConfig = new LocalConfig
            {
                Projects = projects
            };
            await FileSystem.WriteJson(localConfig, path);

            DebugLine(() => "Done");
            return await Run(null, new List<string>(0));
        }

        private async Task RecursivelySubstitute(IReadOnlyCollection<string> files,
            IOctopusAsyncClient client,
            string currentDirectory,
            IReadOnlyDictionary<string, string> variables, GlobalConfig globalConfig)
        {
            var localConfig = await GetLocalConfig(client, currentDirectory, globalConfig);
            if (localConfig?.Projects?.Any() ?? false)
            {
                DebugLine(() => $"Found {Colors.Bold(localConfig.Projects.Count)} projects");
                foreach (var project in localConfig.Projects)
                {
                    DebugLine(() => $"Using project {Colors.Yellow(project)}");
                    var subDirectory = Path.Combine(currentDirectory, project);
                    await RecursivelySubstitute(files, client, subDirectory, variables, globalConfig);
                    DebugLine(() => $"Done with project {Colors.Yellow(project)}");
                }
            }
            else
            {
                await Substitute(files, client, currentDirectory, localConfig, variables);
            }
        }

        private async Task Substitute(
            IReadOnlyCollection<string> files,
            IOctopusAsyncClient client,
            string currentDirectory,
            LocalConfig localConfig,
            IReadOnlyDictionary<string, string> variables)
        {
            var userConfig = await GetUserLocalConfig(currentDirectory);
            var context = await GetVariables(client, localConfig, userConfig, variables);
            var exclude = FileSystem.FindFiles(currentDirectory, localConfig.Exclude?.ToArray()).ToHashSet();
            var paths = files?.Select(Static.MakeAbsolutePath(currentDirectory)).ToList() ??
                        Static.FindOctopusFiles(currentDirectory, localConfig.ConfigFilePattern);
            WriteLine($"Found these files {string.Join(Environment.NewLine, paths)}");
            var maxLength = paths.Any() ? paths.Max(p => p.Length) - currentDirectory.Length : 0;
            DebugLine(() => $"Found {Colors.Bold(paths.Count)} files");
            var excluded = exclude.Intersect(paths).ToHashSet();
            if (excluded.Any())
            {
                DebugLine(() => $"Excluding {Colors.Bold(excluded.Count)} files");
            }

            foreach (var path in paths)
            {
                if (excluded.Contains(path))
                {
                    DebugLine(() => $"{Colors.Yellow("[--]")} {Colors.Cyan(Path.GetFileName(path).PadRight(maxLength))}    {Colors.Yellow("(Excluded)")}");
                }
                else
                {
                    Debug(() => $"[  ] {Colors.Cyan(Path.GetFileName(path).PadRight(maxLength))} -> ");
                    var transformsDefinitions = localConfig.ConfigFilePattern.Select(x => new TransformDefinition(x)).ToList();
                    var activeTransform = transformsDefinitions.First(x => Regex.IsMatch(path, x.TransformPattern));
                    var oldFileName = Path.GetFileName(path);
                    var newFileName = oldFileName.Replace(activeTransform.TransformPattern, activeTransform.SourcePattern).Replace("..", ".");
                    var newPath = Path.Combine(new FileInfo(path).Directory.FullName, newFileName);
                    var result = context.Evaluate(await FileSystem.ReadFile(path), out var error);
                    await FileSystem.WriteFile(newPath, result);
                    Debug(() => $"{Colors.Cyan(Path.GetFileName(newPath))}");
                    DebugLine(() => string.IsNullOrEmpty(error) ? $"\r{Colors.Green("[OK]")}" : $"\r{Colors.Red("[  ]")}\n     {Colors.Red(error)}");
                }
            }
        }

        private async Task<GlobalConfig> GetGlobalConfig()
        {
            var directory = FileSystem.GetAppDataLocalPath();
            var path = Path.Combine(directory, "OctoLocal", "octolocal.json");
            try
            {
                DebugLine(() => $"Using {Colors.Magenta(path)}");
                var globalConfig = await FileSystem.ReadJson<GlobalConfig>(path);
                if (string.IsNullOrWhiteSpace(globalConfig.Server) || string.IsNullOrWhiteSpace(globalConfig.ApiKey))
                {
                    return await CreateGlobalConfig(path);
                }

                return globalConfig;
            }
            catch
            {
                return await CreateGlobalConfig(path);
            }
        }

        private async Task<GlobalConfig> CreateGlobalConfig(string path)
        {
            WriteLine(Colors.Red($"Could not find {Colors.Bold(path)}"));
            WriteLine("Please provide connection information for the Octopus server");
            Write(Colors.Bold("Server: "));
            var server = Console.ReadLine();
            Write(Colors.Bold("ApiKey: "));
            var apiKey = Console.ReadLine();
            var config = new GlobalConfig
            {
                Server = server,
                ApiKey = apiKey
            };
            await FileSystem.WriteJson(config, path);
            WriteLine(Colors.Green($"Saved {Colors.Bold(path)}"));
            return config;
        }

        private async Task<LocalConfig> GetLocalConfig(IOctopusAsyncClient client, string currentDirectory, GlobalConfig globalConfig)
        {
            var path = Path.Combine(currentDirectory, "octolocal.json");
            try
            {
                DebugLine(() => $"Using {Colors.Magenta(path)}");
                var localConfig = await FileSystem.ReadJson<LocalConfig>(path);
                if (localConfig.Projects == null && string.IsNullOrWhiteSpace(localConfig.ProjectId))
                {
                    return await CreateLocalConfig(client, path);
                }

                return localConfig;
            }
            catch
            {
                return await CreateLocalConfig(client, path);
            }
        }

        private async Task<LocalConfig> CreateLocalConfig(IOctopusAsyncClient client, string path)
        {
            var directoryName = FileSystem.GetDirectoryName(path);
            WriteLine();
            WriteLine(Colors.Red($"Could not find config {Colors.Bold(path)}"));
            WriteLine($"Guessing project based on folder name ({Colors.Bold(directoryName)})");
            var projects = await GetAllProjects(client);
            var selectedProject = GuessProject(projects, directoryName);

            WriteLine($"Using {Colors.Bold(selectedProject.Name)} ({Colors.Bold(selectedProject.Id)})");
            var config = new LocalConfig
            {
                ProjectId = selectedProject.Id,
                Variables = new Dictionary<string, string>
                {
                    ["Octopus.Project.Name"] = selectedProject.Name,
                },
                OverrideVariables = new Dictionary<string, string>(),
                Exclude = new List<string>()
            };
            await FileSystem.WriteJson(config, path);
            WriteLine(Colors.Green($"Saved {Colors.Bold(path)}"));
            return config;
        }

        private async Task<IReadOnlyDictionary<string, string>> GetUserLocalConfig(string currentDirectory)
        {
            var path = Path.Combine(currentDirectory, "octolocal.user.json");
            try
            {
                if (!File.Exists(path))
                {
                    return new Dictionary<string, string>();
                }

                var result = await FileSystem.ReadJson<Dictionary<string, string>>(path);
                DebugLine(() => $"Using {Colors.Magenta(path)}");
                return result;
            }
            catch
            {
                return new Dictionary<string, string>();
            }
        }

        private ProjectResource GuessProject(IReadOnlyCollection<ProjectResource> projects, string guess)
        {
            var leven = new Fastenshtein.Levenshtein(guess);
            var topFive = projects.OrderBy(p => leven.Distance(p.Name)).Take(5).ToList();
            var i = 0;
            WriteLine($"Found 5 projects similar to {Colors.Bold(guess)}:");
            foreach (var project in topFive)
            {
                WriteLine($"{Colors.Cyan($"[{++i}]")} {project.Name}");
            }

            Write($"Use number {Colors.Cyan("[ ]")} or hit enter to search for another project name\rUse number {Colors.Cyan("[")}");
            if (int.TryParse(Console.ReadLine()?.Trim(), out var r))
            {
                return topFive[r - 1];
            }
            else
            {
                Write($"Search for project with name similar to: ");
                var name = Console.ReadLine();
                return GuessProject(projects, name);
            }
        }

        private static async Task<IReadOnlyCollection<ProjectResource>> GetAllProjects(IOctopusAsyncClient client)
        {
            var projects = new List<ProjectResource>();
            await client.Repository.Projects.Paginate(page =>
            {
                projects.AddRange(page.Items);
                return true;
            });
            return projects;
        }

        private async Task<VariableDictionary> GetVariables(
            IOctopusAsyncClient client,
            LocalConfig config,
            IReadOnlyDictionary<string, string> userConfig,
            IReadOnlyDictionary<string, string> sessionVariables)
        {
            DebugLine(() => "Getting variables");
            var project = await client.Repository.Projects.Get(config.ProjectId);
            var projectVariables = await GetProjectVariables(client, project);
            DebugLine(() => $"Got {Colors.Bold(projectVariables.Count.ToString().PadLeft(3))} project variables");
            var variableSets = await client.Repository.LibraryVariableSets.Get(project.IncludedLibraryVariableSetIds.ToArray());
            DebugLine(() => $"Found {Colors.Bold(variableSets.Count)} library variable sets");
            var variableSetVariables = await GetLibraryVariables(variableSets, client);

            DebugLine(() => $"Got {Colors.Bold(variableSetVariables.Keys.Concat(projectVariables.Keys).Distinct().Count().ToString().PadLeft(3))} distinct variables");
            return Static.CreateVariables(variableSetVariables, projectVariables, config, userConfig, sessionVariables);
        }

        private static async Task<IReadOnlyDictionary<string, string>> GetProjectVariables(IOctopusAsyncClient client, ProjectResource project)
        {
            var variables = await client.Repository.VariableSets.Get(project.VariableSetId);
            return variables.Variables.ToDictionary();
        }

        private async Task<IReadOnlyDictionary<string, string>> GetLibraryVariables(
            IEnumerable<LibraryVariableSetResource> variableSets, IOctopusAsyncClient client)
        {
            var result = new List<VariableResource>();
            foreach (var variableSet in variableSets)
            {
                var variables = await client.Repository.VariableSets.Get(variableSet.VariableSetId);
                DebugLine(() => $"Got {Colors.Bold(variables.Variables.Count.ToString().PadLeft(3))} variables from {Colors.Bold(variableSet.Name)}");
                result.AddRange(variables.Variables);
            }

            return result.ToDictionary();
        }

        private void Debug(Func<string> message)
        {
            if (!_quiet)
            {
                _console.Write(message());
            }
        }

        private void DebugLine(Func<string> message)
        {
            if (!_quiet)
            {
                _console.WriteLine(message());
            }
        }

        private void WriteLine()
        {
            _console.WriteLine();
        }

        private void WriteLine(string input)
        {
            _console.WriteLine(input);
        }

        private void Write(string input)
        {
            _console.Write(input);
        }
    }
}