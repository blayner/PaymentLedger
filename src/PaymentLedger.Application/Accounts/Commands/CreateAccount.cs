using PaymentLedger.Application.Abstractions;
using PaymentLedger.Application.Messaging;
using PaymentLedger.Domain.Accounts;

namespace PaymentLedger.Application.Accounts.Commands;

public sealed record CreateAccountCommand(string OwnerName, string Currency) : ICommand<Guid>;

public sealed class CreateAccountCommandHandler(IAccountRepository accounts)
    : ICommandHandler<CreateAccountCommand, Guid>
{
    public async Task<Guid> Handle(CreateAccountCommand request, CancellationToken cancellationToken)
    {
        var account = Account.Open(Guid.NewGuid(), request.OwnerName, request.Currency);
        await accounts.SaveAsync([account], cancellationToken);
        return account.Id;
    }
}
