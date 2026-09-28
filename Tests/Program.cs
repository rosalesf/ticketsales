using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using TicketSales.Controllers;
using TicketSales.Data;
using TicketSales.Models;
using TicketSales.Services;

var passed = 0;
void Check(bool value, string name) { if (!value) throw new Exception("FAIL: " + name); Console.WriteLine("PASS: " + name); passed++; }
var now = DateTimeOffset.UtcNow;
var key = RandomNumberGenerator.GetBytes(32);
var secret = "whsec_" + Convert.ToBase64String(key);
var stamp = now.ToUnixTimeSeconds().ToString();
var bytes = Encoding.UTF8.GetBytes("{\"event_type\":\"intent.succeeded\"}");
var signature = "v1," + Convert.ToBase64String(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes("msg_test." + stamp + ".").Concat(bytes).ToArray()));
Check(RecurrenteWebhookSignature.Verify(secret, "msg_test", stamp, signature, bytes, now), "Valid signed webhook");
Check(!RecurrenteWebhookSignature.Verify(secret, "msg_test", stamp, signature, [1, 2], now), "Tampered body rejected");
Check(!RecurrenteWebhookSignature.Verify(secret, "msg_test", stamp, signature, bytes, now.AddMinutes(6)), "Stale signature rejected");
Check(!RecurrenteWebhookSignature.Verify(secret, "other", stamp, signature, bytes, now), "Wrong message ID rejected");
Check(RecurrenteWebhookSignature.Verify(secret, "msg_test", stamp, "v1,broken " + signature, bytes, now), "Signature rotation supported");
Check(!RecurrenteWebhookSignature.Verify("bad", "msg_test", stamp, signature, bytes, now), "Malformed secret rejected");
Check(!RecurrenteWebhookSignature.Verify("whsec_", "msg_test", stamp, signature, bytes, now), "Empty signing key rejected");
var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
    ["Recurrente:SecretKey"] = "sk_test_fake", ["Recurrente:WebhookSecret"] = secret,
    ["Recurrente:PublicBaseUrl"] = "https://tickets.example.test"
}).Build();
var handler = new RecordingHandler();
var realGateway = new RecurrentePaymentGateway(new HttpClient(handler), config);
await realGateway.CreateCheckoutAsync("{}", "order-test", default);
Check(handler.Secret == "sk_test_fake" && handler.Key == "order-test" && handler.Url == "https://app.recurrente.com/api/checkouts", "Authenticated request and idempotency header");
await realGateway.CreateCustomerAsync("{}", "customer-test", default);
Check(handler.Key == "customer-test" && handler.Url == "https://app.recurrente.com/api/customers", "Customer synchronization uses authenticated idempotent request");
Check(!new RecurrentePaymentGateway(new HttpClient(handler), new ConfigurationBuilder().Build()).IsConfigured, "Unconfigured gateway disabled");

// A unique disposable LocalDB database; never use the application's connection string.
var databaseName = "TicketSalesPaymentChecks_" + Guid.NewGuid().ToString("N");
var folder = Path.GetFullPath(Path.Combine("Tests", ".data"));
Directory.CreateDirectory(folder);
var master = new SqlConnectionStringBuilder { DataSource = @"(localdb)\MSSQLLocalDB", InitialCatalog = "master", IntegratedSecurity = true, TrustServerCertificate = true, ConnectTimeout = 20 };
await using var admin = new SqlConnection(master.ConnectionString);
await admin.OpenAsync();
var dataFile = Path.Combine(folder, databaseName + ".mdf").Replace("'", "''");
var logFile = Path.Combine(folder, databaseName + ".ldf").Replace("'", "''");
await using (var create = admin.CreateCommand())
{
    create.CommandText = $"CREATE DATABASE [{databaseName}] ON (NAME=N'{databaseName}', FILENAME=N'{dataFile}') LOG ON (NAME=N'{databaseName}_log', FILENAME=N'{logFile}')";
    await create.ExecuteNonQueryAsync();
}
try
{
    master.InitialCatalog = databaseName;
    var options = new DbContextOptionsBuilder<TicketDbContext>().UseSqlServer(master.ConnectionString).Options;
    await using var db = new TicketDbContext(options);
    await db.Database.EnsureCreatedAsync();
    await db.Database.ExecuteSqlRawAsync("DROP INDEX IX_Orders_PaymentAccessToken ON dbo.Orders; ALTER TABLE dbo.Orders DROP COLUMN PaymentAccessToken; ALTER TABLE dbo.Orders DROP COLUMN ConfirmationEmailSentAt;");
    PaymentSchema.EnsureCreated(db);
    PaymentSchema.EnsureCreated(db);
    Check(true, "Payment schema upgrades existing table and can run repeatedly");
    var ev = new Event { Name = "Test", StartsAt = DateTime.Now.AddDays(2), Status = EventStatuses.Published };
    var type = new TicketType { Event = ev, Name = "General", Price = 10, AvailableTickets = 10 };
    db.TicketTypes.Add(type); await db.SaveChangesAsync();
    var typeId = type.Id; var eventId = ev.Id;
    Order NewOrder(string? token = null) => new() { OrderNumber = "TEST-" + Guid.NewGuid().ToString("N"), PaymentAccessToken = token ?? Guid.NewGuid().ToString("N"), Total = 10,
        Items = [new OrderItem { EventId = eventId, TicketTypeId = typeId, EventName = "Test", TicketTypeName = "General", Quantity = 1, UnitPrice = 10 }] };
    var gateway = new FakeGateway();
    var customers = new FakeCustomerService();
    var service = new PaymentOrderService(db, gateway, config, customers);
    var order = await service.ReserveAsync(NewOrder(), default);
    var duplicate = await service.ReserveAsync(NewOrder(order.PaymentAccessToken), default);
    Check(duplicate.Id == order.Id && await db.TicketTypes.Where(t => t.Id == typeId).Select(t => t.AvailableTickets).SingleAsync() == 9, "Duplicate submission reserves once");
    gateway.FailCreation = true;
    try { await service.EnsureCheckoutAsync(order.Id, default); } catch (HttpRequestException) { }
    gateway.FailCreation = false;
    var checkoutRequest = await db.Orders.AsNoTracking().Where(o => o.Id == order.Id).Select(o => o.CheckoutRequestJson).SingleAsync();
    using (var payload = JsonDocument.Parse(checkoutRequest!))
    {
        var item = payload.RootElement.GetProperty("items")[0];
        Check(item.GetProperty("amount_in_cents").GetInt32() == 1000 && item.GetProperty("payment_method_types")[0].GetString() == "card"
            && item.GetProperty("billing_info_requirement").GetString() == "none" && payload.RootElement.GetProperty("customer_id").GetString() == "cus_test",
            "Checkout preloads customer and leaves only card data");
    }
    await service.EnsureCheckoutAsync(order.Id, default);
    Check(gateway.Requests.Count == 2 && gateway.Requests[0] == gateway.Requests[1], "Uncertain create retries identical request and key");
    gateway.Status = "payment_in_progress";
    await service.SynchronizeAsync(order.Id, default);
    Check(await db.Orders.Where(o => o.Id == order.Id).Select(o => o.InventoryReserved).SingleAsync(), "Pending payment keeps inventory reserved");
    gateway.Status = "paid";
    await service.SynchronizeAsync(order.Id, default);
    await service.SynchronizeAsync(order.Id, default, failed: true);
    Check(await db.Orders.Where(o => o.Id == order.Id).Select(o => o.PaymentStatus).SingleAsync() == "Approved"
        && await db.TicketTypes.Where(t => t.Id == typeId).Select(t => t.AvailableTickets).SingleAsync() == 9, "Success followed by duplicate/failure never decrements twice or downgrades");
    var http = new DefaultHttpContext();
    http.Features.Set<ISessionFeature>(new TestSessionFeature { Session = new MemorySession() });
    var home = new HomeController(db, null!, service, null!, null!, NullLogger<HomeController>.Instance)
        { ControllerContext = new ControllerContext { HttpContext = http } };
    Check(await home.PaymentStatus(order.Id, "wrong-token") is NotFoundResult, "Order status denies unrelated buyer");
    Check(await home.Confirmation(order.Id, "wrong-token") is NotFoundResult, "Order details deny unrelated buyer");
    Check(await home.PaymentStatus(order.Id, order.PaymentAccessToken) is JsonResult, "Private order token allows status access");
    var webhookHttp = new DefaultHttpContext();
    webhookHttp.Request.Body = new MemoryStream(bytes);
    var webhook = new RecurrenteWebhookController(db, service, config)
        { ControllerContext = new ControllerContext { HttpContext = webhookHttp } };
    Check(await webhook.Receive(default) is UnauthorizedResult, "Webhook endpoint denies unsigned requests");
    var expired = await service.ReserveAsync(NewOrder(), default);
    await service.EnsureCheckoutAsync(expired.Id, default);
    gateway.Status = "expired";
    await service.SynchronizeAsync(expired.Id, default);
    await service.SynchronizeAsync(expired.Id, default);
    Check(await db.TicketTypes.Where(t => t.Id == typeId).Select(t => t.AvailableTickets).SingleAsync() == 9, "Expired checkout releases exactly once");
    gateway.Status = "paid";
    await service.SynchronizeAsync(expired.Id, default);
    Check(await db.Orders.Where(o => o.Id == expired.Id).Select(o => o.OrderStatus).SingleAsync() == "PaymentReview", "Late success after release requires review, no ticket issued");
    var wrongAmount = await service.ReserveAsync(NewOrder(), default);
    await service.EnsureCheckoutAsync(wrongAmount.Id, default);
    gateway.Cents = 999;
    await service.SynchronizeAsync(wrongAmount.Id, default);
    Check(await db.Orders.Where(o => o.Id == wrongAmount.Id).Select(o => o.OrderStatus).SingleAsync() == "PaymentReview", "Wrong payment amount cannot approve order");
    gateway.Cents = 1000;
    await db.TicketTypes.Where(t => t.Id == typeId).ExecuteUpdateAsync(s => s.SetProperty(t => t.AvailableTickets, 1));
    async Task<bool> BuyLast()
    {
        await using var isolated = new TicketDbContext(options);
        try { await new PaymentOrderService(isolated, gateway, config, customers).ReserveAsync(NewOrder(), default); return true; }
        catch (InvalidOperationException) { return false; }
    }
    var outcomes = await Task.WhenAll(BuyLast(), BuyLast());
    Check(outcomes.Count(x => x) == 1 && await db.TicketTypes.Where(t => t.Id == typeId).Select(t => t.AvailableTickets).SingleAsync() == 0, "Concurrent buyers cannot oversell last ticket");
    Console.WriteLine($"All {passed} checks passed.");
}
finally
{
    SqlConnection.ClearAllPools();
    if (!databaseName.StartsWith("TicketSalesPaymentChecks_", StringComparison.Ordinal) || databaseName.Length != 57) throw new Exception("Unexpected test database name");
    await using var drop = admin.CreateCommand();
    drop.CommandText = $"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}]";
    await drop.ExecuteNonQueryAsync();
}

sealed class RecordingHandler : HttpMessageHandler
{
    public string? Secret, Key, Url;
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Secret = request.Headers.GetValues("X-SECRET-KEY").Single();
        Key = request.Headers.GetValues("Idempotency-Key").Single(); Url = request.RequestUri!.ToString();
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") });
    }
}
sealed class TestSessionFeature : ISessionFeature
{
    public ISession Session { get; set; } = new MemorySession();
}
sealed class MemorySession : ISession
{
    private readonly Dictionary<string, byte[]> values = [];
    public bool IsAvailable => true;
    public string Id => "test";
    public IEnumerable<string> Keys => values.Keys;
    public void Clear() => values.Clear();
    public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void Remove(string key) => values.Remove(key);
    public void Set(string key, byte[] value) => values[key] = value;
    public bool TryGetValue(string key, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out byte[]? value) => values.TryGetValue(key, out value);
}
sealed class FakeGateway : IPaymentGateway
{
    public bool IsConfigured => true;
    public bool FailCreation;
    public string Status = "unpaid";
    public int Cents = 1000;
    public List<(string, string)> Requests = [];
    public Task<JsonElement> CreateCheckoutAsync(string payload, string idempotencyKey, CancellationToken ct)
    {
        Requests.Add((payload, idempotencyKey));
        if (FailCreation) throw new HttpRequestException("Simulated lost create response");
        var id = "ch_" + idempotencyKey;
        return Task.FromResult(JsonSerializer.SerializeToElement(new { id, checkout_url = "https://app.recurrente.com/checkout-session/" + id }));
    }
    public Task<JsonElement> GetCheckoutAsync(string id, CancellationToken ct) => Task.FromResult(JsonSerializer.SerializeToElement(new {
        id, status = Status, total_in_cents = Cents, currency = "GTQ", payment = new { id = "pa_test" }
    }));
    public Task<JsonElement> UpdateCheckoutAsync(string id, string payload, string idempotencyKey, CancellationToken ct)
        => Task.FromResult(JsonSerializer.SerializeToElement(new { id, status = Status, total_in_cents = Cents }));
    public Task<JsonElement> CreateCustomerAsync(string payload, string idempotencyKey, CancellationToken ct)
        => Task.FromResult(JsonSerializer.SerializeToElement(new { id = "cus_test" }));
    public Task UpdateCustomerAsync(string id, string payload, CancellationToken ct) => Task.CompletedTask;
}
sealed class FakeCustomerService : IRecurrenteCustomerService
{
    public Task SynchronizeAsync(Order order, CancellationToken cancellationToken)
    {
        order.RecurrenteCustomerId = "cus_test";
        return Task.CompletedTask;
    }
}
