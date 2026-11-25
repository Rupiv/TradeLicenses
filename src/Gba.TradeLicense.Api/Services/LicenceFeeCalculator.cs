namespace Gba.TradeLicense.Api.Services
{
    /// <summary>
    /// The existing licence fee rule of the New Licence form (new-licenses.ts calculateFee),
    /// reproduced on the server so the payable amount never comes from the browser:
    ///     sum of trade fees + max(power HP, generator HP) x 100 + 500
    /// No other rule is added.
    /// </summary>
    public static class LicenceFeeCalculator
    {
        public const decimal PowerFeePerHp = 100m;
        public const decimal OcExemptedFee = 500m;
        public const int MaxHpDecimals = 4; // storage precision (DECIMAL(12,4))

        public static decimal Payable(IEnumerable<decimal> tradeFees, decimal powerHp, decimal generatorHp) =>
            decimal.Round(tradeFees.Sum() + Math.Max(powerHp, generatorHp) * PowerFeePerHp + OcExemptedFee, 2);

        /// <summary>Lower bound used for drafts saved before HP was stored (power fee unknown).</summary>
        public static decimal MinimumWithoutHp(IEnumerable<decimal> tradeFees) =>
            decimal.Round(tradeFees.Sum() + OcExemptedFee, 2);

        /// <summary>Null when the value is acceptable; otherwise the message to return.</summary>
        public static string? ValidateHp(decimal? value, string label)
        {
            if (value == null) return null; // not sent: an older client - the stored value is left as it is
            if (value.Value < 0) return $"{label} cannot be negative.";
            if (decimal.Round(value.Value, MaxHpDecimals) != value.Value)
                return $"{label} can have at most {MaxHpDecimals} decimal places.";
            if (value.Value > 99_999_999m) return $"{label} is not a valid value.";
            return null;
        }
    }
}
