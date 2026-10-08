using System.Globalization;

namespace howmuch.Core;

public enum GoodsType { ListEligible, ListExcluded }

public sealed record ImportEstimate(decimal PriceKrw, decimal ShippingKrw, decimal DutyKrw,
    decimal VatKrw, bool IsExempt)
{
    public decimal TaxesKrw => DutyKrw + VatKrw;
    public decimal TotalKrw => PriceKrw + ShippingKrw + TaxesKrw;
}

public static class ImportCalculator
{
    public static decimal ExemptionThresholdUsd(bool shippedFromUsa, GoodsType goodsType) =>
        shippedFromUsa && goodsType == GoodsType.ListEligible ? 200m : 150m;

    // Deliberately unambiguous: dot decimals, optional correctly grouped commas.
    public static bool TryAmount(string text, out decimal amount)
    {
        amount = 0;
        text = text.Trim();
        if (!System.Text.RegularExpressions.Regex.IsMatch(text,
            @"^(?:[0-9]+|[0-9]{1,3}(?:,[0-9]{3})+)(?:\.[0-9]{0,4})?$"))
            return false;
        return decimal.TryParse(text, NumberStyles.AllowDecimalPoint | NumberStyles.AllowThousands,
            CultureInfo.InvariantCulture, out amount) && amount <= 1_000_000_000m;
    }

    public static ImportEstimate Calculate(decimal price, decimal shipping, decimal krwRate,
        decimal usdRate, decimal dutyPercent, bool shippedFromUsa, GoodsType goodsType)
    {
        if (price < 0 || shipping < 0 || krwRate <= 0 || usdRate <= 0 || dutyPercent < 0 || dutyPercent > 100)
            throw new ArgumentOutOfRangeException(nameof(price));
        decimal value = price * krwRate;
        decimal freight = shipping * krwRate;
        bool exempt = price * usdRate <= ExemptionThresholdUsd(shippedFromUsa, goodsType);
        decimal duty = exempt ? 0 : (value + freight) * dutyPercent / 100m;
        decimal vat = exempt ? 0 : (value + freight + duty) * 0.10m;
        // Estimate rounded to won; actual customs assessment uses its own rounding rules.
        return new(value, freight, decimal.Round(duty, 0, MidpointRounding.AwayFromZero),
            decimal.Round(vat, 0, MidpointRounding.AwayFromZero), exempt);
    }
}
