using FluentValidation;

namespace PaymentLedger.Application.Validation;

internal static class RuleExtensions
{
    public static IRuleBuilderOptions<T, string> MustBeCurrencyCode<T>(this IRuleBuilder<T, string> rule) =>
        rule
            .NotEmpty()
            .Length(3)
            .Matches("^[A-Z]{3}$")
            .WithMessage("'{PropertyName}' must be an upper-case ISO 4217 code, e.g. BRL or USD.");

    public static IRuleBuilderOptions<T, decimal> MustBeMonetaryAmount<T>(this IRuleBuilder<T, decimal> rule) =>
        rule
            .GreaterThan(0m)
            .PrecisionScale(18, 2, ignoreTrailingZeros: true);

    public static IRuleBuilderOptions<T, int> MustBePage<T>(this IRuleBuilder<T, int> rule) =>
        rule.GreaterThanOrEqualTo(1);

    public static IRuleBuilderOptions<T, int> MustBePageSize<T>(this IRuleBuilder<T, int> rule) =>
        rule.InclusiveBetween(1, 200);
}
