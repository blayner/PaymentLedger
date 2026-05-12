using PaymentLedger.Domain.Common;
using PaymentLedger.Domain.ValueObjects;

namespace PaymentLedger.Domain.Tests;

public sealed class MoneyTests
{
    [Theory]
    [InlineData("brl")]
    [InlineData("BR")]
    [InlineData("BRLX")]
    [InlineData("")]
    [InlineData("B1L")]
    public void Rejects_invalid_currency_codes(string currency)
    {
        var error = Should.Throw<DomainException>(() => new Money(10m, currency));

        error.Code.ShouldBe("money.invalid_currency");
    }

    [Fact]
    public void Rejects_amounts_with_more_than_two_decimal_places()
    {
        var error = Should.Throw<DomainException>(() => new Money(10.001m, "BRL"));

        error.Code.ShouldBe("money.invalid_precision");
    }

    [Fact]
    public void Accepts_trailing_zeros_beyond_scale()
    {
        var money = new Money(10.500m, "BRL");

        money.Amount.ShouldBe(10.5m);
    }

    [Fact]
    public void Adds_and_subtracts_in_the_same_currency()
    {
        var a = new Money(100.25m, "BRL");
        var b = new Money(0.75m, "BRL");

        (a + b).ShouldBe(new Money(101m, "BRL"));
        (a - b).ShouldBe(new Money(99.5m, "BRL"));
    }

    [Fact]
    public void Refuses_to_combine_different_currencies()
    {
        var error = Should.Throw<DomainException>(() => new Money(1m, "BRL") + new Money(1m, "USD"));

        error.Code.ShouldBe("money.currency_mismatch");
    }

    [Fact]
    public void Compares_amounts()
    {
        var small = new Money(1m, "EUR");
        var large = new Money(2m, "EUR");

        (small < large).ShouldBeTrue();
        (large >= small).ShouldBeTrue();
        small.CompareTo(new Money(1.00m, "EUR")).ShouldBe(0);
    }

    [Fact]
    public void Formats_with_invariant_culture()
    {
        new Money(1234.5m, "USD").ToString().ShouldBe("1234.50 USD");
    }
}
