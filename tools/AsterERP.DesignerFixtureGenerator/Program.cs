using AsterERP.Api.Application.ApplicationDevelopmentCenter;
using SqlSugar;

var arguments = ParseArguments(args);
var databasePath = RequireArgument(arguments, "database");
var tenantId = RequireArgument(arguments, "tenant");
var appCode = RequireArgument(arguments, "app");
var outputDirectory = RequireArgument(arguments, "output");

if (!File.Exists(databasePath))
{
    throw new FileNotFoundException("Designer fixture source database was not found.", databasePath);
}

Directory.CreateDirectory(outputDirectory);
using var database = new SqlSugarClient(new ConnectionConfig
{
    ConnectionString = $"Data Source={databasePath}",
    DbType = DbType.Sqlite,
    IsAutoCloseConnection = true
});

var sources = new DesignerFixtureSourceReader().Read(database, tenantId, appCode);
await new DesignerFixtureGenerator(new DesignerFixtureAnonymizer())
    .GenerateAsync(sources, outputDirectory);

Console.WriteLine($"Generated {sources.Count} fixture(s) in {Path.GetFullPath(outputDirectory)}.");

static Dictionary<string, string> ParseArguments(string[] args)
{
    var arguments = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    for (var index = 0; index < args.Length; index++)
    {
        var argument = args[index];
        if (!argument.StartsWith("--", StringComparison.Ordinal) || index + 1 >= args.Length)
        {
            throw new ArgumentException("Arguments must use --name value format.");
        }

        arguments[argument[2..]] = args[++index];
    }

    return arguments;
}

static string RequireArgument(IReadOnlyDictionary<string, string> arguments, string name)
{
    if (!arguments.TryGetValue(name, out var value) || string.IsNullOrWhiteSpace(value))
    {
        throw new ArgumentException($"Missing required argument --{name}.");
    }

    return value;
}
