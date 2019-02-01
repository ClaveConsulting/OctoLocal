namespace Clave.OctoLocal
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Newtonsoft.Json;

    public static class FileSystem
    {
        public static string GetAppDataLocalPath()
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        }

        public static string GetCurrentDirectory()
        {
            return Directory.GetCurrentDirectory();
        }

        public static string GetDirectoryName(string path)
        {
            return new FileInfo(path).Directory?.Name;
        }

        public static IReadOnlyCollection<string> FindFiles(string currentDirectory, params string[] query)
        {
            if (query == null)
            {
                return new List<string>(0);
            }

            var files = query.SelectMany(ReadAllFilesRecursively).Distinct().ToList();

            return files;

            IEnumerable<string> ReadAllFilesRecursively(string pattern) => Directory.EnumerateFiles(
                currentDirectory,
                new TransformDefinition(pattern).TransformPatternWithWildcard ?? new TransformDefinition(pattern).SourcePatternWithWildcard,
                SearchOption.AllDirectories);
        }

        public static async Task<T> ReadJson<T>(string path)
        {
            var content = await File.ReadAllTextAsync(path);
            return JsonConvert.DeserializeObject<T>(content);
        }

        public static async Task WriteJson<T>(T config, string path)
        {
            var json = JsonConvert.SerializeObject(
                config,
                Formatting.Indented,
                new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore
                });
            Directory.CreateDirectory(new FileInfo(path).DirectoryName);
            await File.WriteAllTextAsync(path, json);
        }

        public static async Task<string> ReadFile(string path)
        {
            return await File.ReadAllTextAsync(path);
        }

        public static async Task WriteFile(string newPath, string result)
        {
            await File.WriteAllTextAsync(newPath, result);
        }
    }
}