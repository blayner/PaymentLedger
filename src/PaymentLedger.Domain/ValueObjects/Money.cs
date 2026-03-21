using System.Globalization;
using PaymentLedger.Domain.Common;

namespace PaymentLedger.Domain.ValueObjects;

/// <summary>
/// Monetary amount in a single currency. Amounts are kept at a fixed scale of two
/// decimal places; anything finer is rejected instead of silently rounded, because
/// silent rounding in a ledger is how money disappears.
/// </summary>
public sealed record Money : IComparable<Money>
{
    public const int Scale = 2;

    public Money(decimal amount, string currency)
    {
        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3 || !currency.All(char.IsAsciiLetterUpper))
        {
            throw new DomainException("money.invalid_currency", $"'{currency}' is not a valid ISO 4217 currency code.");
        }

        if (decimal.Round(amount, Scale) != amount)
        {
            throw new DomainException("money.invalid_precision", $"Amount {amount} has more than {Scale} decimal places.");
        }

        Amount = amount;
        Currency = currency;
    }

    public decimal Amount { get; }

    public string Currency { get; }

    public bool IsZero => Amount == 0m;

    public bool IsPositive => Amount > 0m;

    public bool IsNegative => Amount < 0m;

    public static Money Zero(string currency) => new(0m, currency);

    public static Money operator +(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return new Money(left.Amount + right.Amount, left.Currency);
    }

    public static Money operator -(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return new Money(left.Amount - right.Amount, left.Currency);
    }

    public static bool operator >(Money left, Money right) => left.CompareTo(right) > 0;

    public static bool operator <(Money left, Money right) => left.CompareTo(right) < 0;

    public static bool operator >=(Money left, Money right) => left.CompareTo(right) >= 0;

    public static bool operator <=(Money left, Money right) => left.CompareTo(right) <= 0;

    public int CompareTo(Money? other)
    {
        if (other is null)
        {
            return 1;
        }

        EnsureSameCurrency(this, other);
        return Amount.CompareTo(other.Amount);
    }

    public override string ToString() =>
        $"{Amount.ToString("0.00", CultureInfo.InvariantCulture)} {Currency}";

    private static void EnsureSameCurrency(Money left, Money right)
    {
        if (!string.Equals(left.Currency, right.Currency, StringComparison.Ordinal))
        {
            throw new DomainException(
                "money.currency_mismatch",
                $"Cannot combine amounts in {left.Currency} and {right.Currency}.");
        }
    }
}
