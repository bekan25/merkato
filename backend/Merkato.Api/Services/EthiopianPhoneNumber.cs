using System.Text.RegularExpressions;

namespace Merkato.Api.Services;

public static class EthiopianPhoneNumber
{
    public static bool TryNormalize(string? input, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(input)) return false;

        var value = Regex.Replace(input.Trim(), @"[\s()-]", string.Empty);
        if (value.StartsWith("00", StringComparison.Ordinal)) value = "+" + value[2..];

        if (Regex.IsMatch(value, @"^0[97]\d{8}$"))
            value = "+251" + value[1..];
        else if (Regex.IsMatch(value, @"^251[97]\d{8}$"))
            value = "+" + value;
        else if (Regex.IsMatch(value, @"^\+251[97]\d{8}$"))
        {
            // Already canonical.
        }
        else
        {
            return false;
        }

        normalized = value;
        return true;
    }
}
