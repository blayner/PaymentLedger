using PaymentLedger.Application.Abstractions;
using PaymentLedger.Application.Common;
using PaymentLedger.Application.Messaging;

namespace PaymentLedger.Application.Accounts.Commands;

public sealed record FreezeAccountCommand(Guid AccountId, string Reason) : ICommand;

public sealed record UnfreezeAccountCommand(Guid AccountId) : ICommand;

public sealed record CloseAccountCommand(Guid AccountId) : ICommand;

public sealed class AccountLifecycleHandlers(IAccountRepository accounts) :
    ICommandHandler<FreezeAccountCommand>,
    ICommandHandler<UnfreezeAccountCommand>,
    ICommandHandler<CloseAccountCommand>
{
    public async Task<Unit> Handle(FreezeAccountCommand request, CancellationToken cancellationToken)
    {
        var account = await accounts.GetRequiredAsync(request.AccountId, cancellationToken);
        account.Freeze(request.Reason);
        await accounts.SaveAsync([account], cancellationToken);
        return Unit.Value;
    }

    public async Task<Unit> Handle(UnfreezeAccountCommand request, CancellationToken cancellationToken)
    {
        var account = await accounts.GetRequiredAsync(request.AccountId, cancellationToken);
        account.Unfreeze();
        await accounts.SaveAsync([account], cancellationToken);
        return Unit.Value;
    }

    public async Task<Unit> Handle(CloseAccountCommand request, CancellationToken cancellationToken)
    {
        var account = await accounts.GetRequiredAsync(request.AccountId, cancellationToken);
        account.Close();
        await accounts.SaveAsync([account], cancellationToken);
        return Unit.Value;
    }
}
