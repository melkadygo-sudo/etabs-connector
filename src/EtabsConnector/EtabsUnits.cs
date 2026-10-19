namespace EtabsConnector;

internal static class EtabsUnits
{
    private static readonly IReadOnlyDictionary<string, int> ByName =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["lb_in_F"] = 1,
            ["lb_ft_F"] = 2,
            ["kip_in_F"] = 3,
            ["kip_ft_F"] = 4,
            ["kN_mm_C"] = 5,
            ["kN_m_C"] = 6,
            ["kgf_mm_C"] = 7,
            ["kgf_m_C"] = 8,
            ["N_mm_C"] = 9,
            ["N_m_C"] = 10,
            ["Ton_mm_C"] = 11,
            ["Ton_m_C"] = 12,
            ["kN_cm_C"] = 13,
            ["kgf_cm_C"] = 14,
            ["N_cm_C"] = 15,
            ["Ton_cm_C"] = 16
        };

    public static int Parse(string name)
    {
        if (!ByName.TryGetValue(name, out var value))
            throw new ConnectorException(
                $"Unsupported units '{name}'. Allowed values: {string.Join(", ", ByName.Keys)}");
        return value;
    }

    public static string NameOf(int value) =>
        ByName.FirstOrDefault(pair => pair.Value == value).Key ?? $"Unknown({value})";

    public static IReadOnlyCollection<string> Names => ByName.Keys.ToArray();
}
