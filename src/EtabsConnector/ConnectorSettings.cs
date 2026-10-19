namespace EtabsConnector;

internal sealed record ConnectorSettings(
    bool AllowWrites,
    bool AllowAnalysis,
    IReadOnlyList<string> AllowedRoots,
    int MaximumResultRows)
{
    public static ConnectorSettings FromEnvironment()
    {
        var rootsValue = Environment.GetEnvironmentVariable("ETABS_CONNECTOR_ALLOWED_ROOTS");
        var roots = string.IsNullOrWhiteSpace(rootsValue)
            ? new[] { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ETABS-MCP") }
            : rootsValue.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(Path.GetFullPath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

        var maxRows = 10_000;
        var maxRowsValue = Environment.GetEnvironmentVariable("ETABS_CONNECTOR_MAX_RESULT_ROWS");
        if (int.TryParse(maxRowsValue, out var configured))
            maxRows = Math.Clamp(configured, 100, 50_000);

        return new ConnectorSettings(
            AllowWrites: ReadBoolean("ETABS_CONNECTOR_ALLOW_WRITES", defaultValue: false),
            AllowAnalysis: ReadBoolean("ETABS_CONNECTOR_ALLOW_ANALYSIS", defaultValue: false),
            AllowedRoots: roots,
            MaximumResultRows: maxRows);
    }

    public string RequireAllowedPath(string path, bool createParent)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ConnectorException("A model path is required.");

        var fullPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(path));
        var permitted = AllowedRoots.Any(root => IsWithin(fullPath, root));
        if (!permitted)
            throw new ConnectorException(
                $"Path is outside ETABS_CONNECTOR_ALLOWED_ROOTS: {fullPath}");

        if (!fullPath.EndsWith(".edb", StringComparison.OrdinalIgnoreCase))
            throw new ConnectorException("ETABS model paths must use the .edb extension.");

        if (createParent)
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        return fullPath;
    }

    public string RequireAllowedFileSystemPath(string path, bool createParent)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ConnectorException("A file path is required.");
        var fullPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(path));
        if (!AllowedRoots.Any(root => IsWithin(fullPath, root)))
            throw new ConnectorException($"Path is outside ETABS_CONNECTOR_ALLOWED_ROOTS: {fullPath}");
        if (createParent)
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        return fullPath;
    }
    private static bool IsWithin(string candidate, string root)
    {
        var fullRoot = Path.GetFullPath(root)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var fullCandidate = Path.GetFullPath(candidate);
        return fullCandidate.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase);
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
