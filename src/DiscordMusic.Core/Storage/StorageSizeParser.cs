using System.Globalization;

namespace DiscordMusic.Core.Storage;

public static class StorageSizeParser
{
    private static readonly IReadOnlyDictionary<string, long> Units = new Dictionary<string, long>(
        StringComparer.OrdinalIgnoreCase
    )
    {
        ["B"] = 1,
        ["BYTE"] = 1,
        ["BYTES"] = 1,
        ["KB"] = 1_000,
        ["KIB"] = 1_024,
        ["MB"] = 1_000_000,
        ["MIB"] = 1_048_576,
        ["GB"] = 1_000_000_000,
        ["GIB"] = 1_073_741_824,
        ["TB"] = 1_000_000_000_000,
        ["TIB"] = 1_099_511_627_776,
    };

    public static bool TryParseBytes(string value, out long bytes)
    {
        bytes = 0;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        var numberLength = 0;
        while (
            numberLength < trimmed.Length
            && (
                char.IsDigit(trimmed[numberLength])
                || trimmed[numberLength] == '.'
                || trimmed[numberLength] == ','
            )
        )
        {
            numberLength++;
        }

        if (numberLength == 0)
        {
            return false;
        }

        var numberPart = trimmed[..numberLength].Replace(',', '.');
        var unitPart = trimmed[numberLength..].Trim();
        if (unitPart.Length == 0)
        {
            unitPart = "B";
        }

        if (
            !double.TryParse(
                numberPart,
                NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out var number
            )
            || number < 0
        )
        {
            return false;
        }

        if (!Units.TryGetValue(unitPart, out var multiplier))
        {
            return false;
        }

        bytes = checked((long)Math.Ceiling(number * multiplier));
        return true;
    }
}
