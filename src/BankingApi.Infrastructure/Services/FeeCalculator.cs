using Microsoft.Extensions.Options;

namespace BankingApi.Infrastructure.Services;

/// <summary>
/// A single tier in the fee schedule.
/// MaxAmount = null means "catch-all" (applies to any amount above the last explicit tier).
/// </summary>
public record FeeTier(decimal? MaxAmount, decimal Fee);

public interface IFeeCalculator
{
    /// <summary>
    /// Returns the flat transaction fee for the given transfer amount.
    /// Matches the first tier where amount &lt;= MaxAmount, or the catch-all tier
    /// where MaxAmount is null.
    /// </summary>
    decimal Calculate(decimal amount);
}

public class FeeCalculator : IFeeCalculator
{
    private readonly List<FeeTier> _tiers;

    public FeeCalculator(IOptions<List<FeeTier>> options)
    {
        _tiers = options.Value;

        if (_tiers is null || _tiers.Count == 0)
            throw new InvalidOperationException(
                "FeeSchedule configuration is missing or empty. " +
                "Add a 'FeeSchedule' array to appsettings.json.");

        // Guarantee exactly one catch-all tier exists
        var catchAlls = _tiers.Count(t => t.MaxAmount is null);
        if (catchAlls != 1)
            throw new InvalidOperationException(
                $"FeeSchedule must contain exactly one catch-all tier " +
                $"(MaxAmount: null). Found {catchAlls}.");
    }

    public decimal Calculate(decimal amount)
    {
        // Ordered ascending: bounded tiers first (by MaxAmount), catch-all last
        var matched = _tiers
            .Where(t => t.MaxAmount is null || amount <= t.MaxAmount)
            .OrderBy(t => t.MaxAmount ?? decimal.MaxValue)
            .FirstOrDefault();

        // Should never be null given the catch-all validation in ctor,
        // but guard defensively.
        if (matched is null)
            throw new InvalidOperationException(
                $"No fee tier matched for amount {amount:N2}. " +
                "Ensure a catch-all tier (MaxAmount: null) exists in FeeSchedule.");

        return matched.Fee;
    }
}