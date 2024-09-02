using System.CommandLine;
using System.CommandLine.Binding;

namespace AonGenerator;

class Program
{
    static async Task Main(string[] args)
    {
        var rootCommand = new RootCommand("AoN Generator");

        var dbHost = new Option<string>("--db-host", "Database host");
        dbHost.SetDefaultValue("127.0.0.1");

        var dbDatabase = new Option<string>("--db-database", "Database to load from")
        {
            IsRequired = true,
        };

        var dbUser = new Option<string>("--db-user", "Database user")
        {
            IsRequired = true,
        };

        var dbPassword = new Option<string>("--db-password", "Database password")
        {
            IsRequired = true,
        };

        var outputPath = new Option<string>("--output-path", "Path to output files in")
        {
            IsRequired = true,
        };

        rootCommand.AddOption(dbDatabase);
        rootCommand.AddOption(dbHost);
        rootCommand.AddOption(dbPassword);
        rootCommand.AddOption(dbUser);
        rootCommand.AddOption(outputPath);

        rootCommand.SetHandler((context) =>
        {
#pragma warning disable CS8601 // Possible null reference assignment.
            var options = new Options()
            {
                DbHost = context.ParseResult.GetValueForOption(dbHost),
                DbDatabase = context.ParseResult.GetValueForOption(dbDatabase),
                DbUser = context.ParseResult.GetValueForOption(dbUser),
                DbPassword = context.ParseResult.GetValueForOption(dbPassword),
                OutputPath = context.ParseResult.GetValueForOption(outputPath),
            };
#pragma warning restore CS8601 // Possible null reference assignment.
            Generator.Generate(options);
        });

        await rootCommand.InvokeAsync(args);
    }
}
