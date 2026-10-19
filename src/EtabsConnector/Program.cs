using System.Text;

namespace EtabsConnector;

internal static class Program
{
    [STAThread]
    private static async Task<int> Main(string[] args)
    {
        Console.InputEncoding = Encoding.UTF8;
        Console.OutputEncoding = new UTF8Encoding(false);

        if (!OperatingSystem.IsWindows())
        {
            Console.Error.WriteLine("ETABS Connector requires Windows.");
            return 1;
        }

        try
        {
            var settings = ConnectorSettings.FromEnvironment();
            using var session = new EtabsSession(settings);
            var server = new McpServer(session, settings);
            await server.RunAsync(Console.In, Console.Out);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Fatal connector error: {ex}");
            return 1;
        }
    }
}
