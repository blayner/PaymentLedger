using FluentValidation;
using PaymentLedger.Application.Accounts.Commands;
using PaymentLedger.Application.Accounts.Queries;
using PaymentLedger.Application.Ledger.Queries;

namespace PaymentLedger.Application.Validation;

public sealed class CreateAccountCommandValidator : AbstractValidator<CreateAccountCommand>
{
    public CreateAccountCommandValidator()
    {
        RuleFor(x => x.OwnerName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Currency).MustBeCurrencyCode();
    }
}

public sealed class DepositFundsCommandValidator : AbstractValidator<DepositFundsCommand>
{
    public DepositFundsCommandValidator()
    {
        RuleFor(x => x.AccountId).NotEmpty();
        RuleFor(x => x.TransactionId).NotEmpty();
        RuleFor(x => x.Amount).MustBeMonetaryAmount();
        RuleFor(x => x.Currency).MustBeCurrencyCode();
        RuleFor(x => x.Description).MaximumLength(500);
    }
}

public sealed class WithdrawFundsCommandValidator : AbstractValidator<WithdrawFundsCommand>
{
    public WithdrawFundsCommandValidator()
    {
        RuleFor(x => x.AccountId).NotEmpty();
        RuleFor(x => x.TransactionId).NotEmpty();
        RuleFor(x => x.Amount).MustBeMonetaryAmount();
        RuleFor(x => x.Currency).MustBeCurrencyCode();
        RuleFor(x => x.Description).MaximumLength(500);
    }
}

public sealed class TransferFundsCommandValidator : AbstractValidator<TransferFundsCommand>
{
    public TransferFundsCommandValidator()
    {
        RuleFor(x => x.SourceAccountId).NotEmpty();
        RuleFor(x => x.DestinationAccountId)
            .NotEmpty()
            .NotEqual(x => x.SourceAccountId)
            .WithMessage("Destination account must differ from the source account.");
        RuleFor(x => x.TransactionId).NotEmpty();
        RuleFor(x => x.Amount).MustBeMonetaryAmount();
        RuleFor(x => x.Currency).MustBeCurrencyCode();
        RuleFor(x => x.Description).MaximumLength(500);
    }
}

public sealed class FreezeAccountCommandValidator : AbstractValidator<FreezeAccountCommand>
{
    public FreezeAccountCommandValidator()
    {
        RuleFor(x => x.AccountId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

public sealed class ListAccountsQueryValidator : AbstractValidator<ListAccountsQuery>
{
    public ListAccountsQueryValidator()
    {
        RuleFor(x => x.Page).MustBePage();
        RuleFor(x => x.PageSize).MustBePageSize();
    }
}

public sealed class GetLedgerEntriesQueryValidator : AbstractValidator<GetLedgerEntriesQuery>
{
    public GetLedgerEntriesQueryValidator()
    {
        RuleFor(x => x.AccountId).NotEmpty();
        RuleFor(x => x.Page).MustBePage();
        RuleFor(x => x.PageSize).MustBePageSize();
    }
}
