using System.Net.Http.Json;
using System.Text.Json;
using PaymentLedger.Application.Contracts;

namespace PaymentLedger.Integration.Tests.Infrastructure;

/// <summary>Thin typed wrapper over the HTTP API so tests read as scenarios.</summary>
public sealed class LedgerApiClient(HttpClient http)
{
    public HttpClient Http { get; } = http;

    public async Task<AccountDto> OpenAccountAsync(string owner, string currency = "BRL")
    {
        var response = await Http.PostAsJsonAsync("/api/accounts", new { ownerName = owner, currency });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AccountDto>())!;
    }

    public Task<HttpResponseMessage> DepositAsync(Guid accountId, decimal amount, Guid? idempotencyKey, string currency = "BRL") =>
        SendMoneyAsync($"/api/accounts/{accountId}/deposits", new { amount, currency, description = "deposit" }, idempotencyKey);

    public Task<HttpResponseMessage> WithdrawAsync(Guid accountId, decimal amount, Guid? idempotencyKey, string currency = "BRL") =>
        SendMoneyAsync($"/api/accounts/{accountId}/withdrawals", new { amount, currency, description = "withdrawal" }, idempotencyKey);

    public Task<HttpResponseMessage> TransferAsync(Guid from, Guid to, decimal amount, Guid? idempotencyKey) =>
        SendMoneyAsync(
            "/api/transfers",
            new { sourceAccountId = from, destinationAccountId = to, amount, currency = "BRL", description = "transfer" },
            idempotencyKey);

    public async Task<AccountBalanceDto> GetBalanceAsync(Guid accountId) =>
        (await Http.GetFromJsonAsync<AccountBalanceDto>($"/api/accounts/{accountId}/balance"))!;

    public async Task<JsonElement> GetTrialBalanceAsync() =>
        await Http.GetFromJsonAsync<JsonElement>("/api/ledger/trial-balance");

    private async Task<HttpResponseMessage> SendMoneyAsync(string url, object body, Guid? idempotencyKey)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) };
        if (idempotencyKey is { } key)
        {
            request.Headers.Add("Idempotency-Key", key.ToString());
        }

        return await Http.SendAsync(request);
    }
}
