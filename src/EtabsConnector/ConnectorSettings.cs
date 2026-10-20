namespace EtabsConnector;

internal sealed record ConnectorSettings(
    bool AllowWrites,
    bool AllowAnalysis,
    int MaximumResultRows)
{
    public static ConnectorSettings FromEnvironment()
    {
        var maxRows = 10_000;
        var maxRowsValue = Environment.GetEnvironmentVariable("ETABS_CONNECTOR_MAX_RESULT_ROWS");
        if (int.TryParse(maxRowsValue, out var configured))
            maxRows = Math.Clamp(configured, 100, 50_000);

        return new ConnectorSettings(
            AllowWrites: ReadBoolean("ETABS_CONNECTOR_ALLOW_WRITES", defaultValue: false),
            AllowAnalysis: ReadBoolean("ETABS_CONNECTOR_ALLOW_ANALYSIS", defaultValue: false),
            MaximumResultRows: maxRows);
    }

    public string ValidateModelPath(string path, bool createParent)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ConnectorException("A model path is required.");

        var fullPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(path));
        if (!fullPath.EndsWith(".edb", StringComparison.OrdinalIgnoreCase))
            throw new ConnectorException("ETABS model paths must use the .edb extension.");

        if (createParent)
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        return fullPath;
    }

    public string ValidateFileSystemPath(string path, bool createParent)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ConnectorException("A file path is required.");
        var fullPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(path));
        if (createParent)
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        return fullPath;
    }
    private static bool ReadBoolean(string name, bool defaultValue)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return value?.Trim().ToLowerInvariant() switch
        {
            "1" or "true" or "yes" or "on" => true,
            "0" or "false" or "no" or "off" => false,
            _ => defaultValue
        };
    }
}
