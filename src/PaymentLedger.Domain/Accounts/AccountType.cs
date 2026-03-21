namespace PaymentLedger.Domain.Accounts;

/// <summary>
/// Accounting classification from the platform's point of view.
/// Customer wallets are liabilities (money the platform owes its customers);
/// settlement accounts are assets (money the platform actually holds).
/// </summary>
public enum AccountType
{
    Asset = 1,
    Liability = 2,
}
