namespace Clave.OctoLocal
{
    using System;
    using Microsoft.Extensions.CommandLineUtils;

    internal static class Program
    {
        public static int Main(string[] args)
        {
            var app = new CommandLineApplication();

            var quiet = app.Option("-q|--quiet", "Don't log what is going on", CommandOptionType.NoValue);
            var files = app.Option("-f|--files", "List of files to process", CommandOptionType.MultipleValue);
            var variables = app.Option("-v|--variable", "Specify variable", CommandOptionType.MultipleValue);

            app.HelpOption("-?|-h|--help");
            app.ExtendedHelpText = "\nMore information online: https://github.com/ClaveConsulting/OctoLocal";

            var console = AnsiConsole.GetOutput();
            console.WriteLine();

            app.Command("init", initApp =>
            {
                initApp.Description = "Initialize this solution";

                var projects = initApp.Option("-p|--project", "list projects to initialize with", CommandOptionType.MultipleValue);

                initApp.OnExecute(() => new App(console, quiet.HasValue()).Init(projects.ValuesOrEmpty()));
            });

            app.OnExecute(() => new App(console, quiet.HasValue()).Run(files.ValuesOrNull(), variables.ValuesOrEmpty()));

            try
            {
                return app.Execute(args);
            }
            catch (Exception ex)
            {
                do
                {
                    Console.Error.WriteLine(ex.Message);
                    Console.Error.WriteLine(ex.StackTrace);
                }
                while ((ex = ex.InnerException) != null);

                return -1;
            }
        }
    }
}
