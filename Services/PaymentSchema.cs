using Microsoft.EntityFrameworkCore;
using TicketSales.Data;

namespace TicketSales.Services;

public static class PaymentSchema
{
    public static void EnsureCreated(TicketDbContext db) => db.Database.ExecuteSqlRaw("""
        IF COL_LENGTH(N'dbo.Orders', N'PaymentAccessToken') IS NULL
            ALTER TABLE dbo.Orders ADD PaymentAccessToken NVARCHAR(64) NULL;
        IF COL_LENGTH(N'dbo.Orders', N'RecurrenteCheckoutId') IS NULL
            ALTER TABLE dbo.Orders ADD RecurrenteCheckoutId NVARCHAR(128) NULL;
        IF COL_LENGTH(N'dbo.Orders', N'RecurrenteCustomerId') IS NULL
            ALTER TABLE dbo.Orders ADD RecurrenteCustomerId NVARCHAR(128) NULL;
        IF COL_LENGTH(N'dbo.Orders', N'RecurrenteCheckoutUrl') IS NULL
            ALTER TABLE dbo.Orders ADD RecurrenteCheckoutUrl NVARCHAR(MAX) NULL;
        IF COL_LENGTH(N'dbo.Orders', N'CheckoutRequestJson') IS NULL
            ALTER TABLE dbo.Orders ADD CheckoutRequestJson NVARCHAR(MAX) NULL;
        IF COL_LENGTH(N'dbo.Orders', N'ReservationExpiresAt') IS NULL
            ALTER TABLE dbo.Orders ADD ReservationExpiresAt DATETIME2 NULL;
        IF COL_LENGTH(N'dbo.Orders', N'InventoryReserved') IS NULL
            ALTER TABLE dbo.Orders ADD InventoryReserved BIT NOT NULL CONSTRAINT DF_Orders_InventoryReserved DEFAULT 0;
        IF COL_LENGTH(N'dbo.Orders', N'ConfirmationEmailSentAt') IS NULL
            ALTER TABLE dbo.Orders ADD ConfirmationEmailSentAt DATETIME2 NULL;
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Orders_PaymentAccessToken' AND object_id = OBJECT_ID(N'dbo.Orders'))
            EXEC(N'CREATE UNIQUE INDEX IX_Orders_PaymentAccessToken ON dbo.Orders(PaymentAccessToken) WHERE PaymentAccessToken IS NOT NULL');
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Orders_RecurrenteCheckoutId' AND object_id = OBJECT_ID(N'dbo.Orders'))
            EXEC(N'CREATE UNIQUE INDEX IX_Orders_RecurrenteCheckoutId ON dbo.Orders(RecurrenteCheckoutId) WHERE RecurrenteCheckoutId IS NOT NULL');
        """);
}
