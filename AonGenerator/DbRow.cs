namespace AonGenerator;

internal class DbRow
{
    public Dictionary<string, bool?> Bools { get; set; } = [];
    public Dictionary<string, DateTime?> DateTimes { get; set; } = [];
    public Dictionary<string, decimal?> Decimals { get; set; } = [];
    public Dictionary<string, int?> Ints { get; set; } = [];
    public Dictionary<string, string?> Strings { get; set; } = [];

    public DbRow Clone()
    {
        DbRow row = new();

        row.Bools = Bools.ToDictionary(r => r.Key, r => r.Value);
        row.DateTimes = DateTimes.ToDictionary(r => r.Key, r => r.Value);
        row.Decimals = Decimals.ToDictionary(r => r.Key, r => r.Value);
        row.Ints = Ints.ToDictionary(r => r.Key, r => r.Value);
        row.Strings = Strings.ToDictionary(r => r.Key, r => r.Value);

        return row;
    }
}
