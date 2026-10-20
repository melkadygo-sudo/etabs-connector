using System.Text.Json;

namespace EtabsConnector;

internal sealed class McpServer
{
    private const string ProtocolVersion = "2025-11-25";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly EtabsSession _session;
    private readonly ConnectorSettings _settings;

    public McpServer(EtabsSession session, ConnectorSettings settings)
    {
        _session = session;
        _settings = settings;
    }

    public async Task RunAsync(TextReader input, TextWriter output)
    {
        while (await input.ReadLineAsync() is { } line)
        {
            // Windows PowerShell can prefix redirected UTF-8 input with a BOM.
            line = line.TrimStart('\uFEFF');
            if (string.IsNullOrWhiteSpace(line))
                continue;

            JsonDocument? document = null;
            try
            {
                document = JsonDocument.Parse(line);
                var root = document.RootElement;
                if (!root.TryGetProperty("method", out var methodElement))
                    continue;

                var method = methodElement.GetString() ?? string.Empty;
                var hasId = root.TryGetProperty("id", out var id);
                if (!hasId)
                    continue;

                object result = method switch
                {
                    "initialize" => Initialize(root),
                    "ping" => new { },
                    "tools/list" => new
                    {
                        tools = ToolCatalog.All.Select(tool => new
                        {
                            name = tool.Name,
                            description = tool.Description,
                            inputSchema = tool.InputSchema,
                            annotations = ToolAnnotations(tool.Name)
                        })
                    },
                    "tools/call" => await CallToolAsync(root),
                    _ => throw new RpcMethodNotFoundException(method)
                };

                await WriteResultAsync(output, id.Clone(), result);
            }
            catch (JsonException ex)
            {
                await WriteErrorAsync(output, null, -32700, "Parse error", ex.Message);
            }
            catch (RpcMethodNotFoundException ex)
            {
                var id = TryGetId(document);
                await WriteErrorAsync(output, id, -32601, "Method not found", ex.Message);
            }
            catch (Exception ex)
            {
                var id = TryGetId(document);
                await WriteErrorAsync(output, id, -32603, "Internal error", ex.Message);
            }
            finally
            {
                document?.Dispose();
            }
        }
    }

    private object Initialize(JsonElement request)
    {
        var requested = request.TryGetProperty("params", out var parameters)
            && parameters.TryGetProperty("protocolVersion", out var version)
            ? version.GetString()
            : null;

        var negotiated = requested is "2025-11-25" or "2025-06-18" or "2025-03-26"
            ? requested
            : ProtocolVersion;

        return new
        {
            protocolVersion = negotiated,
            capabilities = new { tools = new { listChanged = false } },
            serverInfo = new { name = "etabs-connector", version = "0.1.4" },
            instructions =
                "Connect read-only first. For write or analysis tasks, use the user-selected open model or an optional working copy; unlock automatically when required. Discover and invoke the full installed ETABS 23 API through the generic gateway. " +
                $"Writes enabled: {_settings.AllowWrites}. Analysis enabled: {_settings.AllowAnalysis}."
        };
    }

    private async Task<object> CallToolAsync(JsonElement request)
    {
        if (!request.TryGetProperty("params", out var parameters)
            || !parameters.TryGetProperty("name", out var nameElement))
            throw new ConnectorException("tools/call requires params.name.");

        var name = nameElement.GetString() ?? string.Empty;
        var arguments = parameters.TryGetProperty("arguments", out var argumentElement)
            ? argumentElement
            : JsonDocument.Parse("{}").RootElement.Clone();

        try
        {
            var data = await ToolCatalog.InvokeAsync(name, arguments, _session);
            var json = JsonSerializer.Serialize(data, JsonOptions);
            return new
            {
                content = new[] { new { type = "text", text = json } },
                structuredContent = data,
                isError = false
            };
        }
        catch (ConnectorException ex)
        {
            return new
            {
                content = new[] { new { type = "text", text = ex.Message } },
                isError = true
            };
        }
    }

    private static object ToolAnnotations(string name)
    {
        var readOnly = name is "get_capabilities" or "connect"
            or "get_model_info" or "list_api_methods" or "describe_api_method"
            or "list_load_definitions" or "list_objects"
            or "list_properties" or "get_frame_section" or "get_area_property"
            or "get_point_restraint"
            or "get_joint_reactions" or "get_frame_forces"
            or "get_global_y_displacement_envelope" or "get_joint_global_y_displacement";
        return new
        {
            readOnlyHint = readOnly,
            destructiveHint = name == "invoke_api_method",
            idempotentHint = readOnly || name == "set_present_units",
            openWorldHint = false
        };
    }

    private static async Task WriteResultAsync(
        TextWriter output, JsonElement id, object result)
    {
        var message = new { jsonrpc = "2.0", id, result };
        await output.WriteLineAsync(JsonSerializer.Serialize(message, JsonOptions));
        await output.FlushAsync();
    }

    private static JsonElement? TryGetId(JsonDocument? document)
    {
        if (document is not null
            && document.RootElement.TryGetProperty("id", out var id))
            return id.Clone();
        return null;
    }

    private static async Task WriteErrorAsync(
        TextWriter output,
        JsonElement? id,
        int code,
        string message,
        string detail)
    {
        var response = new
        {
            jsonrpc = "2.0",
            id,
            error = new { code, message, data = new { detail } }
        };
        await output.WriteLineAsync(JsonSerializer.Serialize(response, JsonOptions));
        await output.FlushAsync();
    }

    private sealed class RpcMethodNotFoundException : Exception
    {
        public RpcMethodNotFoundException(string method)
            : base($"Unsupported MCP method: {method}")
        {
        }
    }
}
