using System.Text.Json;

namespace EtabsConnector;

internal sealed record ToolDefinition(string Name, string Description, object InputSchema);

internal static class ToolCatalog
{
    public static readonly IReadOnlyList<ToolDefinition> All =
    [
        Tool("get_capabilities",
            "Report ETABS connector permissions, limits, and supported operations.",
            Schema()),
        Tool("list_api_methods",
            "Search all methods exposed by the installed ETABS 23 API assembly.",
            Schema(
                Property("query", "string", "Optional case-insensitive interface, method, or signature filter."),
                Property("max_results", "integer", "Maximum method records to return."))),
        Tool("describe_api_method",
            "Describe overloads, parameter types, ref/out behavior, and inferred access for one ETABS API method.",
            Schema(
                Property("interface_name", "string", "ETABS API interface name, such as cFrameObj."),
                Property("method_name", "string", "Exact ETABS API method name."),
                required: ["interface_name", "method_name"])),
        Tool("invoke_api_method",
            "Invoke any discoverable ETABS 23 API method through a typed reflection gateway with permission, unlock, path, destructive-action, and result-size guards.",
            Schema(
                Property("object_path", "string", "Reachable ETABS object path, such as SapModel.FrameObj or SapModel.Results.Setup."),
                Property("method_name", "string", "Exact ETABS API method name."),
                new KeyValuePair<string, object>("parameters", new { type = "object", description = "Arguments keyed by ETABS parameter name. Omitted ref/out values receive safe defaults.", additionalProperties = true }),
                new KeyValuePair<string, object>("operation_kind", new { type = "string", description = "Requested access classification; the connector may raise it conservatively.", @enum = new[] { "read", "write", "analysis" }, @default = "read" }),
                Property("overload", "integer", "Zero-based overload index from describe_api_method."),
                Property("confirm_destructive", "boolean", "Required for delete, replace, initialize, import, save, open, or application lifecycle methods."),
                required: ["object_path", "method_name"])),        Tool("connect",
            "Attach to a running ETABS 23 instance, optionally starting ETABS when unavailable.",
            Schema(Property("start_if_missing", "boolean", "Start ETABS if no running instance is found."))),
        Tool("open_model",
            "Open an ETABS .edb model from an allowed local directory.",
            Schema(
                Property("path", "string", "Absolute path to the ETABS model."),
                Property("confirm_replace_active", "boolean", "Confirm replacement of any model already open in ETABS."),
                required: ["path"])),
        Tool("get_model_info",
            "Read ETABS version, model path, lock state, units, object counts, and working-copy state.",
            Schema()),
        Tool("list_load_definitions",
            "List load cases and response combinations in the active model.",
            Schema()),
        Tool("list_objects",
            "List ETABS point, frame, or area object names with a bounded result count.",
            Schema(
                Property("object_type", "string", "point, frame, or area."),
                Property("max_results", "integer", "Maximum names to return."),
                required: ["object_type"])),
        Tool("list_properties",
            "List ETABS frame or area property names with a bounded result count.",
            Schema(
                Property("property_type", "string", "frame or area."),
                Property("max_results", "integer", "Maximum names to return."),
                required: ["property_type"])),
        Tool("get_frame_section",
            "Read the assigned section and auto-select list for one frame object.",
            Schema(Property("frame_name", "string", "Exact ETABS frame object name."), required: ["frame_name"])),
        Tool("get_area_property",
            "Read the assigned property for one area object.",
            Schema(Property("area_name", "string", "Exact ETABS area object name."), required: ["area_name"])),
        Tool("get_point_restraint",
            "Read U1, U2, U3, R1, R2, R3 restraints for one point object.",
            Schema(Property("point_name", "string", "Exact ETABS point object name."), required: ["point_name"])),
        Tool("save_working_copy",
            "Optionally save and activate a verified working copy before write or analysis operations.",
            Schema(Property("path", "string", "Absolute .edb destination in an allowed directory."), required: ["path"])),
        Tool("set_present_units",
            "Change ETABS present units on the selected active model and verify the result.",
            Schema(Property("units", "string", $"One of: {string.Join(", ", EtabsUnits.Names)}"), required: ["units"])),
        Tool("set_frame_section",
            "Assign a frame section to one frame object on the selected active model.",
            Schema(
                Property("frame_name", "string", "Exact ETABS frame object name."),
                Property("section_name", "string", "Exact existing frame section property name."),
                required: ["frame_name", "section_name"])),
        Tool("clone_wall_property",
            "Clone an existing ETABS wall property on the selected active model and change only its thickness.",
            Schema(
                Property("source_name", "string", "Exact existing ETABS wall property name."),
                Property("target_name", "string", "Exact new ETABS wall property name."),
                Property("thickness", "number", "New thickness in the current present length units."),
                required: ["source_name", "target_name", "thickness"])),        Tool("set_group_area_property",
            "Assign one existing area property to every area object in one exact ETABS group on the selected active model and verify each area.",
            Schema(
                Property("group_name", "string", "Exact existing ETABS group name."),
                Property("property_name", "string", "Exact existing ETABS area property name."),
                required: ["group_name", "property_name"])),        Tool("set_area_property",
            "Assign an area property to one area object on the selected active model.",
            Schema(
                Property("area_name", "string", "Exact ETABS area object name."),
                Property("property_name", "string", "Exact existing area property name."),
                required: ["area_name", "property_name"])),
        Tool("set_point_restraint",
            "Set U1, U2, U3, R1, R2, R3 restraints for one point on the selected active model.",
            Schema(
                Property("point_name", "string", "Exact ETABS point object name."),
                new KeyValuePair<string, object>("restraint", new
                {
                    type = "array",
                    description = "Six booleans ordered U1, U2, U3, R1, R2, R3.",
                    items = new { type = "boolean" },
                    minItems = 6,
                    maxItems = 6
                }),
                required: ["point_name", "restraint"])),
        Tool("run_analysis",
            "Run ETABS analysis on the selected active model. Requires analysis permission.",
            Schema()),
        Tool("get_joint_global_y_displacement",
            "Read global-Y joint displacement for one named point and analyzed load case.",
            Schema(
                Property("point_name", "string", "Exact ETABS point object name."),
                Property("case_name", "string", "Exact analyzed ETABS load case name."),
                required: ["point_name", "case_name"])),        Tool("get_global_y_displacement_envelope",
            "Read the maximum positive, minimum negative, and maximum absolute global-Y joint displacement for one analyzed load case.",
            Schema(Property("case_name", "string", "Exact analyzed ETABS load case name."), required: ["case_name"])),        Tool("get_joint_reactions",
            "Extract joint reactions for named points and selected cases or combinations.",
            ResultSchema("point_names", "ETABS point object names.")),
        Tool("get_frame_forces",
            "Extract frame forces at ETABS output stations for named frames and selected cases or combinations.",
            ResultSchema("frame_names", "ETABS frame object names."))
    ];

    public static IReadOnlyList<string> Names => All.Select(tool => tool.Name).ToArray();

    public static async Task<object> InvokeAsync(
        string name,
        JsonElement arguments,
        EtabsSession session)
    {
        await Task.Yield();
        return name switch
        {
            "get_capabilities" => session.GetCapabilities(),
            "connect" => session.Connect(OptionalBool(arguments, "start_if_missing", false)),
            "list_api_methods" => session.ListApiMethods(
                OptionalString(arguments, "query", string.Empty),
                OptionalInt(arguments, "max_results", 2_000)),
            "describe_api_method" => session.DescribeApiMethod(
                RequiredString(arguments, "interface_name"),
                RequiredString(arguments, "method_name")),
            "invoke_api_method" => session.InvokeApiMethod(
                RequiredString(arguments, "object_path"),
                RequiredString(arguments, "method_name"),
                OptionalInt(arguments, "overload", 0),
                OptionalObject(arguments, "parameters"),
                OptionalString(arguments, "operation_kind", "read"),
                OptionalBool(arguments, "confirm_destructive", false)),
            "open_model" => session.OpenModel(
                RequiredString(arguments, "path"),
                OptionalBool(arguments, "confirm_replace_active", false)),
            "get_model_info" => session.GetModelInfo(),
            "list_load_definitions" => session.ListLoadDefinitions(),
            "list_objects" => session.ListObjects(
                RequiredString(arguments, "object_type"),
                OptionalInt(arguments, "max_results", 2_000)),
            "list_properties" => session.ListProperties(
                RequiredString(arguments, "property_type"),
                OptionalInt(arguments, "max_results", 2_000)),
            "get_frame_section" => session.GetFrameSection(RequiredString(arguments, "frame_name")),
            "get_area_property" => session.GetAreaProperty(RequiredString(arguments, "area_name")),
            "get_point_restraint" => session.GetPointRestraint(RequiredString(arguments, "point_name")),
            "save_working_copy" => session.SaveWorkingCopy(RequiredString(arguments, "path")),
            "set_present_units" => session.SetPresentUnits(RequiredString(arguments, "units")),
            "set_frame_section" => session.SetFrameSection(
                RequiredString(arguments, "frame_name"),
                RequiredString(arguments, "section_name")),
            "clone_wall_property" => session.CloneWallProperty(
                RequiredString(arguments, "source_name"),
                RequiredString(arguments, "target_name"),
                RequiredDouble(arguments, "thickness")),
            "set_group_area_property" => session.SetGroupAreaProperty(
                RequiredString(arguments, "group_name"),
                RequiredString(arguments, "property_name")),
            "set_area_property" => session.SetAreaProperty(
                RequiredString(arguments, "area_name"),
                RequiredString(arguments, "property_name")),
            "set_point_restraint" => session.SetPointRestraint(
                RequiredString(arguments, "point_name"),
                RequiredBoolArray(arguments, "restraint")),
            "run_analysis" => session.RunAnalysis(),
            "get_joint_global_y_displacement" => session.GetJointGlobalYDisplacement(
                RequiredString(arguments, "point_name"),
                RequiredString(arguments, "case_name")),
            "get_global_y_displacement_envelope" => session.GetGlobalYDisplacementEnvelope(
                RequiredString(arguments, "case_name")),
            "get_joint_reactions" => session.GetJointReactions(
                RequiredStringArray(arguments, "point_names"),
                OptionalStringArray(arguments, "cases"),
                OptionalStringArray(arguments, "combinations"),
                OptionalInt(arguments, "max_results", 2_000)),
            "get_frame_forces" => session.GetFrameForces(
                RequiredStringArray(arguments, "frame_names"),
                OptionalStringArray(arguments, "cases"),
                OptionalStringArray(arguments, "combinations"),
                OptionalInt(arguments, "max_results", 2_000)),
            _ => throw new ConnectorException($"Unknown tool: {name}")
        };
    }

    private static ToolDefinition Tool(string name, string description, object schema) =>
        new(name, description, schema);

    private static object ResultSchema(string objectField, string objectDescription) =>
        Schema(
            ArrayProperty(objectField, objectDescription),
            ArrayProperty("cases", "Load case names to select for output."),
            ArrayProperty("combinations", "Response combination names to select for output."),
            new KeyValuePair<string, object>("max_results", new
            {
                type = "integer",
                description = "Maximum returned result rows.",
                minimum = 1,
                maximum = 50_000,
                @default = 2_000
            }),
            required: [objectField]);

    private static object Schema(
        KeyValuePair<string, object>? property1 = null,
        KeyValuePair<string, object>? property2 = null,
        KeyValuePair<string, object>? property3 = null,
        KeyValuePair<string, object>? property4 = null,
        KeyValuePair<string, object>? property5 = null,
        KeyValuePair<string, object>? property6 = null,
        string[]? required = null)
    {
        var properties = new Dictionary<string, object>();
        foreach (var property in new[] { property1, property2, property3, property4, property5, property6 })
            if (property.HasValue)
                properties[property.Value.Key] = property.Value.Value;

        return new
        {
            type = "object",
            properties,
            required = required ?? [],
            additionalProperties = false
        };
    }

    private static KeyValuePair<string, object> Property(
        string name, string type, string description) =>
        new(name, new { type, description });

    private static KeyValuePair<string, object> ArrayProperty(
        string name, string description) =>
        new(name, new
        {
            type = "array",
            description,
            items = new { type = "string" },
            minItems = 1
        });

    private static string RequiredString(JsonElement arguments, string name)
    {
        if (!arguments.TryGetProperty(name, out var value)
            || value.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(value.GetString()))
            throw new ConnectorException($"Required string argument missing: {name}");
        return value.GetString()!;
    }

    private static string[] RequiredStringArray(JsonElement arguments, string name)
    {
        var values = OptionalStringArray(arguments, name);
        if (values.Length == 0)
            throw new ConnectorException($"Required string array argument missing: {name}");
        return values;
    }

    private static string[] OptionalStringArray(JsonElement arguments, string name)
    {
        if (!arguments.TryGetProperty(name, out var value)
            || value.ValueKind == JsonValueKind.Null)
            return [];
        if (value.ValueKind != JsonValueKind.Array)
            throw new ConnectorException($"{name} must be an array of strings.");
        return value.EnumerateArray()
            .Select(item => item.ValueKind == JsonValueKind.String
                ? item.GetString()
                : throw new ConnectorException($"{name} must contain only strings."))
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Cast<string>()
            .ToArray();
    }

    private static bool[] RequiredBoolArray(JsonElement arguments, string name)
    {
        if (!arguments.TryGetProperty(name, out var value)
            || value.ValueKind != JsonValueKind.Array)
            throw new ConnectorException($"Required boolean array argument missing: {name}");
        return value.EnumerateArray()
            .Select(item => item.ValueKind is JsonValueKind.True or JsonValueKind.False
                ? item.GetBoolean()
                : throw new ConnectorException($"{name} must contain only booleans."))
            .ToArray();
    }

    private static double RequiredDouble(JsonElement arguments, string name)
    {
        if (!arguments.TryGetProperty(name, out var value)
            || value.ValueKind != JsonValueKind.Number
            || !value.TryGetDouble(out var number))
            throw new ConnectorException($"Required numeric argument missing: {name}");
        return number;
    }
    private static string OptionalString(JsonElement arguments, string name, string fallback) =>
        arguments.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? fallback
            : fallback;

    private static JsonElement OptionalObject(JsonElement arguments, string name)
    {
        if (!arguments.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
            return JsonDocument.Parse("{}").RootElement.Clone();
        if (value.ValueKind != JsonValueKind.Object)
            throw new ConnectorException($"{name} must be an object.");
        return value.Clone();
    }
    private static bool OptionalBool(JsonElement arguments, string name, bool fallback) =>
        arguments.TryGetProperty(name, out var value)
        && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : fallback;

    private static int OptionalInt(JsonElement arguments, string name, int fallback) =>
        arguments.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetInt32(out var number)
            ? number
            : fallback;
}
