using ETABSv1;
using System.Reflection;
using System.Text.Json;

namespace EtabsConnector;

internal static class GenericApiGateway
{
    private static readonly Assembly ApiAssembly = typeof(cSapModel).Assembly;

    public static object ListMethods(string? query, int maxResults)
    {
        var needle = query?.Trim() ?? string.Empty;
        var rows = ApiAssembly.GetTypes()
            .Where(type => type.IsInterface && type.Namespace == "ETABSv1")
            .SelectMany(type => type.GetMethods()
                .Where(method => !method.IsSpecialName)
                .Select(method => new
                {
                    interface_name = type.Name,
                    method_name = method.Name,
                    signature = Signature(method),
                    inferred_operation_kind = InferOperationKind(type.Name, method.Name)
                }))
            .Where(row => needle.Length == 0
                || row.interface_name.Contains(needle, StringComparison.OrdinalIgnoreCase)
                || row.method_name.Contains(needle, StringComparison.OrdinalIgnoreCase)
                || $"{row.interface_name}.{row.method_name}".Contains(needle, StringComparison.OrdinalIgnoreCase)
                || row.signature.Contains(needle, StringComparison.OrdinalIgnoreCase))
            .OrderBy(row => row.interface_name)
            .ThenBy(row => row.method_name)
            .ToArray();
        var limit = Math.Clamp(maxResults, 1, 50_000);
        return new
        {
            installed_api_version = "ETABS 23",
            interface_count = ApiAssembly.GetTypes().Count(type => type.IsInterface && type.Namespace == "ETABSv1"),
            matched_method_count = rows.Length,
            returned_count = Math.Min(rows.Length, limit),
            truncated = rows.Length > limit,
            methods = rows.Take(limit).ToArray()
        };
    }

    public static object DescribeMethod(string interfaceName, string methodName)
    {
        var type = ResolveInterface(interfaceName);
        var methods = type.GetMethods()
            .Where(method => !method.IsSpecialName && method.Name.Equals(methodName, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (methods.Length == 0)
            throw new ConnectorException($"ETABS API method not found: {type.Name}.{methodName}");
        return new
        {
            interface_name = type.Name,
            method_name = methodName,
            overload_count = methods.Length,
            overloads = methods.Select((method, index) => new
            {
                overload = index,
                signature = Signature(method),
                return_type = FriendlyType(method.ReturnType),
                inferred_operation_kind = InferOperationKind(type.Name, method.Name),
                parameters = method.GetParameters().Select(parameter => new
                {
                    name = parameter.Name,
                    type = FriendlyType(Unwrap(parameter.ParameterType)),
                    by_reference = parameter.ParameterType.IsByRef,
                    output_only = parameter.IsOut,
                    optional = parameter.IsOptional,
                    default_value = parameter.HasDefaultValue ? Normalize(parameter.DefaultValue, 100) : null
                }).ToArray()
            }).ToArray()
        };
    }

    public static string InferOperationKind(string interfaceName, string methodName)
    {
        if (interfaceName.Equals("cAnalyze", StringComparison.OrdinalIgnoreCase)
            && (methodName.Contains("RunAnalysis", StringComparison.OrdinalIgnoreCase)
                || methodName.Contains("CreateAnalysisModel", StringComparison.OrdinalIgnoreCase)))
            return "analysis";
        if (methodName.StartsWith("Get", StringComparison.OrdinalIgnoreCase)
            || methodName.StartsWith("Count", StringComparison.OrdinalIgnoreCase)
            || methodName.StartsWith("Is", StringComparison.OrdinalIgnoreCase))
            return "read";
        return "write";
    }

    public static object Invoke(
        cOAPI? application,
        cSapModel sapModel,
        string objectPath,
        string methodName,
        int overload,
        JsonElement parameters,
        int maximumResultRows)
    {
        var (instance, declaredType) = ResolveObject(application, sapModel, objectPath);
        var methods = declaredType.GetMethods()
            .Where(method => !method.IsSpecialName && method.Name.Equals(methodName, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (methods.Length == 0)
            throw new ConnectorException($"ETABS API method not found on {declaredType.Name}: {methodName}");
        if (overload < 0 || overload >= methods.Length)
            throw new ConnectorException($"overload must be between 0 and {methods.Length - 1}.");
        if (parameters.ValueKind is not JsonValueKind.Object and not JsonValueKind.Undefined)
            throw new ConnectorException("parameters must be a JSON object keyed by ETABS parameter name.");

        var method = methods[overload];
        var definitions = method.GetParameters();
        var values = new object?[definitions.Length];
        for (var index = 0; index < definitions.Length; index++)
        {
            var definition = definitions[index];
            var targetType = Unwrap(definition.ParameterType);
            if (parameters.ValueKind == JsonValueKind.Object
                && parameters.TryGetProperty(definition.Name!, out var supplied))
                values[index] = ConvertJson(supplied, targetType);
            else if (definition.HasDefaultValue)
                values[index] = definition.DefaultValue;
            else
                values[index] = DefaultValue(targetType);
        }

        object? returnValue;
        try
        {
            returnValue = method.Invoke(instance, values);
        }
        catch (TargetInvocationException exception)
        {
            throw new ConnectorException(
                $"ETABS API invocation failed: {declaredType.Name}.{method.Name}: {exception.InnerException?.Message ?? exception.Message}",
                exception.InnerException ?? exception);
        }

        if (returnValue is int returnCode
            && !method.Name.StartsWith("Count", StringComparison.OrdinalIgnoreCase)
            && returnCode != 0)
            throw new ConnectorException($"CSI API call failed: {declaredType.Name}.{method.Name}, return code {returnCode}.");

        var output = definitions.Select((definition, index) => new
        {
            name = definition.Name,
            type = FriendlyType(Unwrap(definition.ParameterType)),
            by_reference = definition.ParameterType.IsByRef,
            value = Normalize(values[index], maximumResultRows)
        }).ToArray();
        return new
        {
            object_path = objectPath,
            interface_name = declaredType.Name,
            method_name = method.Name,
            overload,
            signature = Signature(method),
            return_value = Normalize(returnValue, maximumResultRows),
            parameters = output
        };
    }

    private static (object Instance, Type DeclaredType) ResolveObject(cOAPI? application, cSapModel sapModel, string objectPath)
    {
        var segments = objectPath.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0)
            throw new ConnectorException("object_path is required, for example SapModel.FrameObj.");
        object current;
        Type currentType;
        var index = 0;
        if (segments[0].Equals("Application", StringComparison.OrdinalIgnoreCase))
        {
            current = application ?? throw new ConnectorException("ETABS application object is unavailable.");
            currentType = typeof(cOAPI);
            index = 1;
        }
        else
        {
            current = sapModel;
            currentType = typeof(cSapModel);
            if (segments[0].Equals("SapModel", StringComparison.OrdinalIgnoreCase))
                index = 1;
        }
        for (; index < segments.Length; index++)
        {
            var property = currentType.GetProperties()
                .FirstOrDefault(item => item.Name.Equals(segments[index], StringComparison.OrdinalIgnoreCase));
            if (property is null)
                throw new ConnectorException($"ETABS API object path segment not found: {currentType.Name}.{segments[index]}");
            current = property.GetValue(current)
                ?? throw new ConnectorException($"ETABS API object is unavailable: {currentType.Name}.{property.Name}");
            currentType = property.PropertyType;
        }
        return (current, currentType);
    }

    private static Type ResolveInterface(string name)
    {
        var normalized = name.StartsWith("ETABSv1.", StringComparison.OrdinalIgnoreCase) ? name : $"ETABSv1.{name}";
        return ApiAssembly.GetType(normalized, throwOnError: false, ignoreCase: true)
            ?? throw new ConnectorException($"ETABS API interface not found: {name}");
    }

    private static object? ConvertJson(JsonElement value, Type targetType)
    {
        if (value.ValueKind == JsonValueKind.Null)
            return null;
        if (targetType == typeof(string)) return value.GetString() ?? string.Empty;
        if (targetType == typeof(bool)) return value.GetBoolean();
        if (targetType == typeof(int)) return value.GetInt32();
        if (targetType == typeof(long)) return value.GetInt64();
        if (targetType == typeof(double)) return value.GetDouble();
        if (targetType == typeof(float)) return value.GetSingle();
        if (targetType == typeof(short)) return value.GetInt16();
        if (targetType == typeof(byte)) return value.GetByte();
        if (targetType.IsEnum)
            return value.ValueKind == JsonValueKind.String
                ? Enum.Parse(targetType, value.GetString()!, ignoreCase: true)
                : Enum.ToObject(targetType, value.GetInt32());
        if (targetType.IsArray)
        {
            if (value.ValueKind != JsonValueKind.Array)
                throw new ConnectorException($"Expected an array for {FriendlyType(targetType)}.");
            var elementType = targetType.GetElementType()!;
            var items = value.EnumerateArray().Select(item => ConvertJson(item, elementType)).ToArray();
            var result = Array.CreateInstance(elementType, items.Length);
            for (var index = 0; index < items.Length; index++) result.SetValue(items[index], index);
            return result;
        }
        return JsonSerializer.Deserialize(value.GetRawText(), targetType);
    }

    private static object? DefaultValue(Type type)
    {
        if (type == typeof(string)) return string.Empty;
        if (type.IsArray) return Array.CreateInstance(type.GetElementType()!, 0);
        return type.IsValueType ? Activator.CreateInstance(type) : null;
    }

    private static object? Normalize(object? value, int maximumRows)
    {
        if (value is null || value is string || value is bool || value is char) return value;
        var type = value.GetType();
        if (type.IsEnum) return new { name = value.ToString(), value = Convert.ToInt32(value) };
        if (value is Array array)
        {
            var limit = Math.Min(array.Length, Math.Max(1, maximumRows));
            return new
            {
                total_count = array.Length,
                returned_count = limit,
                truncated = array.Length > limit,
                values = Enumerable.Range(0, limit).Select(index => Normalize(array.GetValue(index), maximumRows)).ToArray()
            };
        }
        if (value is byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal)
            return value;
        return value.ToString();
    }

    private static Type Unwrap(Type type) => type.IsByRef ? type.GetElementType()! : type;
    private static string FriendlyType(Type type) => type.IsArray
        ? $"{FriendlyType(type.GetElementType()!)}[]"
        : type.IsEnum ? type.Name : type.Name;
    private static string Signature(MethodInfo method) =>
        $"{FriendlyType(method.ReturnType)} {method.Name}({string.Join(", ", method.GetParameters().Select(parameter => $"{(parameter.IsOut ? "out " : parameter.ParameterType.IsByRef ? "ref " : string.Empty)}{FriendlyType(Unwrap(parameter.ParameterType))} {parameter.Name}"))})";
}