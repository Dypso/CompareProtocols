using CommandLine;
using Equipment.Simulator.Protocols;
using Equipment.Simulator.Services;

var parser = new Parser(with => with.HelpWriter = Console.Out);
await parser.ParseArguments<SimulatorOptions>(args)
    .WithParsedAsync(RunSimulator);

async Task RunSimulator(SimulatorOptions opts)
{
    try
    {
        var logger = LoggerFactory.Create(builder =>
            builder.AddConsole())
            .CreateLogger<Program>();

        var simulator = new EquipmentSimulator(
            opts.StartId,
            opts.Count,
            opts.Protocol,
            logger);

        await simulator.RunAsync(CancellationToken.None);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: {ex.Message}");
        Environment.Exit(1);
    }
}

public class SimulatorOptions
{
    [Option('p', "protocol", Required = true, 
        HelpText = "Protocol to use (mqtt, http2, grpc)")]
    public string Protocol { get; set; } = string.Empty;

    [Option('s', "start-id", Required = true,
        HelpText = "Starting equipment ID")]
    public int StartId { get; set; }

    [Option('c', "count", Required = true,
        HelpText = "Number of equipment instances to simulate")]
    public int Count { get; set; }
}