using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using TicketSales.Data;
using TicketSales.Services;

var builder = WebApplication.CreateBuilder(args);
if (builder.Environment.IsEnvironment("Tunnel"))
    builder.WebHost.UseStaticWebAssets();

builder.Services.AddControllersWithViews();
// The local tunnel connects through loopback; keep the framework's trusted-proxy defaults.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);
builder.Services.AddDbContext<TicketDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("App")));
builder.Services.AddDbContext<AdminIdentityDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("App")));
builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
{
    options.Password.RequiredLength = 12;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.User.RequireUniqueEmail = false;
})
    .AddEntityFrameworkStores<AdminIdentityDbContext>()
    .AddDefaultTokenProviders();
builder.Services.Configure<DataProtectionTokenProviderOptions>(options =>
    options.TokenLifespan = TimeSpan.FromMinutes(builder.Configuration.GetValue<int?>("Account:PasswordResetTokenMinutes") ?? 30));
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/admin/login";
    options.AccessDeniedPath = "/admin/access-denied";
});
builder.Services.AddDistributedMemoryCache();
builder.Services.AddDataProtection().SetApplicationName("VentaTickets");
builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".TicketSales.Cart";
    options.IdleTimeout = TimeSpan.FromHours(2);
});
builder.Services.AddHttpClient<IPaymentGateway, RecurrentePaymentGateway>(client => client.Timeout = TimeSpan.FromSeconds(20));
builder.Services.AddScoped<IRecurrenteCustomerService, RecurrenteCustomerService>();
builder.Services.AddScoped<PaymentOrderService>();
builder.Services.AddHostedService<PaymentRecoveryWorker>();
builder.Services.AddScoped<IOrderEmailSender, AzureCommunicationOrderEmailSender>();
builder.Services.AddScoped<IAccountEmailSender, AzureCommunicationAccountEmailSender>();
builder.Services.AddSingleton<TicketSales.Services.IPersonalDataProtector, PersonalDataProtector>();
builder.Services.AddSingleton<IOrderQrCodeGenerator, OrderQrCodeGenerator>();
builder.Services.AddScoped<IAdminMenuAuthorizationService, AdminMenuAuthorizationService>();

var app = builder.Build();
app.UseForwardedHeaders();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TicketDbContext>();
    var identityDb = scope.ServiceProvider.GetRequiredService<AdminIdentityDbContext>();
    db.Database.EnsureCreated();
    PaymentSchema.EnsureCreated(db);
    db.Database.ExecuteSqlRaw("""
        IF COL_LENGTH(N'dbo.Events', N'Impulsar') IS NULL
            ALTER TABLE dbo.Events ADD Impulsar BIT NOT NULL CONSTRAINT DF_Events_Impulsar DEFAULT 0;
        IF COL_LENGTH(N'dbo.Events', N'BannerBase64') IS NULL
            ALTER TABLE dbo.Events ADD BannerBase64 NVARCHAR(MAX) NULL;
        IF COL_LENGTH(N'dbo.Events', N'BannerContentType') IS NULL
            ALTER TABLE dbo.Events ADD BannerContentType NVARCHAR(100) NULL;
        IF COL_LENGTH(N'dbo.Events', N'Status') IS NULL
            ALTER TABLE dbo.Events ADD Status NVARCHAR(20) NOT NULL CONSTRAINT DF_Events_Status DEFAULT N'Publicado';
        IF COL_LENGTH(N'dbo.Orders', N'InvoiceType') IS NULL
            ALTER TABLE dbo.Orders ADD InvoiceType NVARCHAR(3) NOT NULL CONSTRAINT DF_Orders_InvoiceType DEFAULT N'CF';
        IF COL_LENGTH(N'dbo.Orders', N'InvoiceNit') IS NULL
            ALTER TABLE dbo.Orders ADD InvoiceNit NVARCHAR(512) NULL;
        IF COL_LENGTH(N'dbo.Orders', N'InvoiceName') IS NULL
            ALTER TABLE dbo.Orders ADD InvoiceName NVARCHAR(512) NULL;
        IF COL_LENGTH(N'dbo.Orders', N'CustomerUserId') IS NULL
            ALTER TABLE dbo.Orders ADD CustomerUserId NVARCHAR(450) NULL;
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Orders_CustomerUserId' AND object_id = OBJECT_ID(N'dbo.Orders'))
            CREATE INDEX IX_Orders_CustomerUserId ON dbo.Orders(CustomerUserId);
        IF COL_LENGTH(N'dbo.Orders', N'AdditionalInfo') IS NOT NULL
            ALTER TABLE dbo.Orders DROP COLUMN AdditionalInfo;
        IF OBJECT_ID(N'dbo.PurchaseConfirmationEmailTemplates', N'U') IS NULL
            CREATE TABLE dbo.PurchaseConfirmationEmailTemplates (Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_PurchaseConfirmationEmailTemplates PRIMARY KEY, Html NVARCHAR(MAX) NOT NULL, UpdatedAt DATETIME2 NOT NULL);
        """);
    // Los valores protegidos ocupan más espacio que el teléfono original.
    db.Database.ExecuteSqlRaw("""
        IF OBJECT_ID(N'dbo.Orders', N'U') IS NOT NULL
           AND COL_LENGTH(N'dbo.Orders', N'CustomerPhone') BETWEEN 1 AND 1023
            ALTER TABLE dbo.Orders ALTER COLUMN CustomerPhone NVARCHAR(512) NOT NULL;
        """);
    SeedData.Initialize(db);
    await AdminIdentitySchema.EnsureCreatedAsync(identityDb);
    db.Database.ExecuteSqlRaw("""
        IF OBJECT_ID(N'dbo.AspNetUsers', N'U') IS NOT NULL
           AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Orders_AspNetUsers_CustomerUserId')
            ALTER TABLE dbo.Orders ADD CONSTRAINT FK_Orders_AspNetUsers_CustomerUserId
                FOREIGN KEY (CustomerUserId) REFERENCES dbo.AspNetUsers(Id) ON DELETE SET NULL;
        """);
    await AdminIdentitySeed.InitializeAsync(scope.ServiceProvider, builder.Configuration);
}

app.Run();
