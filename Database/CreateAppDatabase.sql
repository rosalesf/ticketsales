
IF OBJECT_ID(N'dbo.Events', N'U') IS NULL
CREATE TABLE dbo.Events (Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Events PRIMARY KEY, Name NVARCHAR(MAX) NOT NULL, Description NVARCHAR(MAX) NOT NULL, Venue NVARCHAR(MAX) NOT NULL, StartsAt DATETIME2 NOT NULL, Price DECIMAL(18,2) NOT NULL, AvailableTickets INT NOT NULL, Impulsar BIT NOT NULL CONSTRAINT DF_Events_Impulsar DEFAULT 0, BannerBase64 NVARCHAR(MAX) NULL, BannerContentType NVARCHAR(100) NULL);
GO
IF COL_LENGTH(N'dbo.Events', N'Impulsar') IS NULL ALTER TABLE dbo.Events ADD Impulsar BIT NOT NULL CONSTRAINT DF_Events_Impulsar_Upgrade DEFAULT 0;
IF COL_LENGTH(N'dbo.Events', N'BannerBase64') IS NULL ALTER TABLE dbo.Events ADD BannerBase64 NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.Events', N'BannerContentType') IS NULL ALTER TABLE dbo.Events ADD BannerContentType NVARCHAR(100) NULL;
IF COL_LENGTH(N'dbo.Events', N'Status') IS NULL ALTER TABLE dbo.Events ADD Status NVARCHAR(20) NOT NULL CONSTRAINT DF_Events_Status_Upgrade DEFAULT N'Publicado';
GO
IF OBJECT_ID(N'dbo.TicketTypes', N'U') IS NULL
CREATE TABLE dbo.TicketTypes (Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TicketTypes PRIMARY KEY, EventId INT NOT NULL, Name NVARCHAR(60) NOT NULL, Price DECIMAL(18,2) NOT NULL, AvailableTickets INT NOT NULL, CONSTRAINT FK_TicketTypes_Events FOREIGN KEY (EventId) REFERENCES dbo.Events(Id) ON DELETE CASCADE);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'UX_TicketTypes_EventId_Name' AND object_id=OBJECT_ID(N'dbo.TicketTypes')) CREATE UNIQUE INDEX UX_TicketTypes_EventId_Name ON dbo.TicketTypes(EventId, Name);
GO
IF OBJECT_ID(N'dbo.Orders', N'U') IS NULL
CREATE TABLE dbo.Orders (Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Orders PRIMARY KEY, CustomerName NVARCHAR(MAX) NOT NULL, CustomerEmail NVARCHAR(MAX) NOT NULL, CustomerUserId NVARCHAR(450) NULL, CustomerPhone NVARCHAR(512) NOT NULL CONSTRAINT DF_Orders_CustomerPhone DEFAULT N'', InvoiceType NVARCHAR(3) NOT NULL CONSTRAINT DF_Orders_InvoiceType DEFAULT N'CF', InvoiceNit NVARCHAR(512) NULL, InvoiceName NVARCHAR(512) NULL, AcceptedTerms BIT NOT NULL CONSTRAINT DF_Orders_AcceptedTerms DEFAULT 0, OrderNumber NVARCHAR(40) NOT NULL, CreatedAt DATETIME2 NOT NULL, Total DECIMAL(18,2) NOT NULL, PaymentStatus NVARCHAR(30) NOT NULL, PaymentReference NVARCHAR(MAX) NULL, OrderStatus NVARCHAR(30) NOT NULL CONSTRAINT DF_Orders_OrderStatus DEFAULT N'PendingPayment');
GO
IF COL_LENGTH(N'dbo.Orders',N'CustomerPhone') IS NULL ALTER TABLE dbo.Orders ADD CustomerPhone NVARCHAR(50) NOT NULL CONSTRAINT DF_Orders_CustomerPhone_Upgrade DEFAULT N'';
IF COL_LENGTH(N'dbo.Orders',N'CustomerUserId') IS NULL ALTER TABLE dbo.Orders ADD CustomerUserId NVARCHAR(450) NULL;
IF COL_LENGTH(N'dbo.Orders',N'AdditionalInfo') IS NOT NULL ALTER TABLE dbo.Orders DROP COLUMN AdditionalInfo;
IF COL_LENGTH(N'dbo.Orders',N'AcceptedTerms') IS NULL ALTER TABLE dbo.Orders ADD AcceptedTerms BIT NOT NULL CONSTRAINT DF_Orders_AcceptedTerms_Upgrade DEFAULT 0;
IF COL_LENGTH(N'dbo.Orders',N'OrderNumber') IS NULL ALTER TABLE dbo.Orders ADD OrderNumber NVARCHAR(40) NULL;
IF COL_LENGTH(N'dbo.Orders',N'OrderStatus') IS NULL ALTER TABLE dbo.Orders ADD OrderStatus NVARCHAR(30) NOT NULL CONSTRAINT DF_Orders_OrderStatus_Upgrade DEFAULT N'PendingPayment';
IF COL_LENGTH(N'dbo.Orders',N'InvoiceType') IS NULL ALTER TABLE dbo.Orders ADD InvoiceType NVARCHAR(3) NOT NULL CONSTRAINT DF_Orders_InvoiceType_Upgrade DEFAULT N'CF';
IF COL_LENGTH(N'dbo.Orders',N'InvoiceNit') IS NULL ALTER TABLE dbo.Orders ADD InvoiceNit NVARCHAR(512) NULL;
IF COL_LENGTH(N'dbo.Orders',N'InvoiceName') IS NULL ALTER TABLE dbo.Orders ADD InvoiceName NVARCHAR(512) NULL;
IF COL_LENGTH(N'dbo.Orders',N'CustomerPhone') BETWEEN 1 AND 1023 ALTER TABLE dbo.Orders ALTER COLUMN CustomerPhone NVARCHAR(512) NOT NULL;
IF COL_LENGTH(N'dbo.Orders',N'PaymentAccessToken') IS NULL ALTER TABLE dbo.Orders ADD PaymentAccessToken NVARCHAR(64) NULL;
IF COL_LENGTH(N'dbo.Orders',N'RecurrenteCheckoutId') IS NULL ALTER TABLE dbo.Orders ADD RecurrenteCheckoutId NVARCHAR(128) NULL;
IF COL_LENGTH(N'dbo.Orders',N'RecurrenteCustomerId') IS NULL ALTER TABLE dbo.Orders ADD RecurrenteCustomerId NVARCHAR(128) NULL;
IF COL_LENGTH(N'dbo.Orders',N'RecurrenteCheckoutUrl') IS NULL ALTER TABLE dbo.Orders ADD RecurrenteCheckoutUrl NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.Orders',N'CheckoutRequestJson') IS NULL ALTER TABLE dbo.Orders ADD CheckoutRequestJson NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.Orders',N'ReservationExpiresAt') IS NULL ALTER TABLE dbo.Orders ADD ReservationExpiresAt DATETIME2 NULL;
IF COL_LENGTH(N'dbo.Orders',N'InventoryReserved') IS NULL ALTER TABLE dbo.Orders ADD InventoryReserved BIT NOT NULL CONSTRAINT DF_Orders_InventoryReserved_Upgrade DEFAULT 0;
IF COL_LENGTH(N'dbo.Orders',N'ConfirmationEmailSentAt') IS NULL ALTER TABLE dbo.Orders ADD ConfirmationEmailSentAt DATETIME2 NULL;
GO
UPDATE dbo.Orders SET OrderNumber=CONCAT(N'LEGACY-',Id) WHERE OrderNumber IS NULL OR OrderNumber=N'';
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.Orders') AND name=N'OrderNumber' AND is_nullable=1) ALTER TABLE dbo.Orders ALTER COLUMN OrderNumber NVARCHAR(40) NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'UX_Orders_OrderNumber' AND object_id=OBJECT_ID(N'dbo.Orders')) CREATE UNIQUE INDEX UX_Orders_OrderNumber ON dbo.Orders(OrderNumber);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_Orders_CustomerUserId' AND object_id=OBJECT_ID(N'dbo.Orders')) CREATE INDEX IX_Orders_CustomerUserId ON dbo.Orders(CustomerUserId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_Orders_PaymentAccessToken' AND object_id=OBJECT_ID(N'dbo.Orders')) CREATE UNIQUE INDEX IX_Orders_PaymentAccessToken ON dbo.Orders(PaymentAccessToken) WHERE PaymentAccessToken IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_Orders_RecurrenteCheckoutId' AND object_id=OBJECT_ID(N'dbo.Orders')) CREATE UNIQUE INDEX IX_Orders_RecurrenteCheckoutId ON dbo.Orders(RecurrenteCheckoutId) WHERE RecurrenteCheckoutId IS NOT NULL;
GO
IF OBJECT_ID(N'dbo.PurchaseConfirmationEmailTemplates', N'U') IS NULL
CREATE TABLE dbo.PurchaseConfirmationEmailTemplates (Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_PurchaseConfirmationEmailTemplates PRIMARY KEY, Html NVARCHAR(MAX) NOT NULL, UpdatedAt DATETIME2 NOT NULL);
GO
IF OBJECT_ID(N'dbo.OrderItems', N'U') IS NULL
CREATE TABLE dbo.OrderItems (Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_OrderItems PRIMARY KEY, OrderId INT NOT NULL, EventId INT NOT NULL, EventName NVARCHAR(MAX) NOT NULL, TicketTypeId INT NOT NULL CONSTRAINT DF_OrderItems_TicketTypeId DEFAULT 0, TicketTypeName NVARCHAR(60) NOT NULL CONSTRAINT DF_OrderItems_TicketTypeName DEFAULT N'General', UnitPrice DECIMAL(18,2) NOT NULL, Quantity INT NOT NULL, CONSTRAINT FK_OrderItems_Orders_OrderId FOREIGN KEY (OrderId) REFERENCES dbo.Orders(Id) ON DELETE CASCADE);
GO
IF COL_LENGTH(N'dbo.OrderItems',N'TicketTypeId') IS NULL ALTER TABLE dbo.OrderItems ADD TicketTypeId INT NOT NULL CONSTRAINT DF_OrderItems_TicketTypeId_Upgrade DEFAULT 0;
IF COL_LENGTH(N'dbo.OrderItems',N'TicketTypeName') IS NULL ALTER TABLE dbo.OrderItems ADD TicketTypeName NVARCHAR(60) NOT NULL CONSTRAINT DF_OrderItems_TicketTypeName_Upgrade DEFAULT N'General';
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_OrderItems_OrderId' AND object_id=OBJECT_ID(N'dbo.OrderItems')) CREATE INDEX IX_OrderItems_OrderId ON dbo.OrderItems(OrderId);
GO
IF NOT EXISTS (SELECT 1 FROM dbo.Events)
INSERT INTO dbo.Events (Name,Description,Venue,StartsAt,Price,AvailableTickets,Impulsar) VALUES
(N'Noche de Jazz',N'Una velada íntima con músicos locales.',N'Teatro Nacional',DATEADD(HOUR,20,DATEADD(DAY,10,CAST(CAST(GETDATE() AS DATE) AS DATETIME2))),225.00,80,1),
(N'Festival Sabores',N'Gastronomía, música y experiencias para toda la familia.',N'Parque Central',DATEADD(HOUR,11,DATEADD(DAY,18,CAST(CAST(GETDATE() AS DATE) AS DATETIME2))),150.00,300,1),
(N'Tech Summit 2026',N'Conferencias prácticas sobre innovación y tecnología.',N'Centro de Convenciones',DATEADD(HOUR,8,DATEADD(DAY,28,CAST(CAST(GETDATE() AS DATE) AS DATETIME2))),475.00,120,0);
GO
INSERT INTO dbo.TicketTypes (EventId,Name,Price,AvailableTickets) SELECT e.Id,N'General',e.Price,e.AvailableTickets FROM dbo.Events e WHERE NOT EXISTS(SELECT 1 FROM dbo.TicketTypes t WHERE t.EventId=e.Id AND t.Name=N'General');
INSERT INTO dbo.TicketTypes (EventId,Name,Price,AvailableTickets) SELECT e.Id,N'Preferencial',ROUND(e.Price*1.35,2),e.AvailableTickets/3 FROM dbo.Events e WHERE NOT EXISTS(SELECT 1 FROM dbo.TicketTypes t WHERE t.EventId=e.Id AND t.Name=N'Preferencial');
INSERT INTO dbo.TicketTypes (EventId,Name,Price,AvailableTickets) SELECT e.Id,N'VIP',ROUND(e.Price*1.75,2),e.AvailableTickets/5 FROM dbo.Events e WHERE NOT EXISTS(SELECT 1 FROM dbo.TicketTypes t WHERE t.EventId=e.Id AND t.Name=N'VIP');
GO

/* ASP.NET Core Identity, perfiles de clientes y permisos de administración. */
IF OBJECT_ID(N'dbo.AspNetRoles', N'U') IS NULL
CREATE TABLE dbo.AspNetRoles (Id NVARCHAR(450) NOT NULL CONSTRAINT PK_AspNetRoles PRIMARY KEY, Name NVARCHAR(256) NULL, NormalizedName NVARCHAR(256) NULL, ConcurrencyStamp NVARCHAR(MAX) NULL);
IF OBJECT_ID(N'dbo.AspNetUsers', N'U') IS NULL
CREATE TABLE dbo.AspNetUsers (Id NVARCHAR(450) NOT NULL CONSTRAINT PK_AspNetUsers PRIMARY KEY, UserName NVARCHAR(256) NULL, NormalizedUserName NVARCHAR(256) NULL, Email NVARCHAR(256) NULL, NormalizedEmail NVARCHAR(256) NULL, EmailConfirmed BIT NOT NULL, PasswordHash NVARCHAR(MAX) NULL, SecurityStamp NVARCHAR(MAX) NULL, ConcurrencyStamp NVARCHAR(MAX) NULL, PhoneNumber NVARCHAR(MAX) NULL, PhoneNumberConfirmed BIT NOT NULL, TwoFactorEnabled BIT NOT NULL, LockoutEnd DATETIMEOFFSET NULL, LockoutEnabled BIT NOT NULL, AccessFailedCount INT NOT NULL);
IF OBJECT_ID(N'dbo.AspNetRoleClaims', N'U') IS NULL
CREATE TABLE dbo.AspNetRoleClaims (Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AspNetRoleClaims PRIMARY KEY, RoleId NVARCHAR(450) NOT NULL, ClaimType NVARCHAR(MAX) NULL, ClaimValue NVARCHAR(MAX) NULL, CONSTRAINT FK_AspNetRoleClaims_AspNetRoles_RoleId FOREIGN KEY (RoleId) REFERENCES dbo.AspNetRoles(Id) ON DELETE CASCADE);
IF OBJECT_ID(N'dbo.AspNetUserClaims', N'U') IS NULL
CREATE TABLE dbo.AspNetUserClaims (Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AspNetUserClaims PRIMARY KEY, UserId NVARCHAR(450) NOT NULL, ClaimType NVARCHAR(MAX) NULL, ClaimValue NVARCHAR(MAX) NULL, CONSTRAINT FK_AspNetUserClaims_AspNetUsers_UserId FOREIGN KEY (UserId) REFERENCES dbo.AspNetUsers(Id) ON DELETE CASCADE);
IF OBJECT_ID(N'dbo.AspNetUserLogins', N'U') IS NULL
CREATE TABLE dbo.AspNetUserLogins (LoginProvider NVARCHAR(450) NOT NULL, ProviderKey NVARCHAR(450) NOT NULL, ProviderDisplayName NVARCHAR(MAX) NULL, UserId NVARCHAR(450) NOT NULL, CONSTRAINT PK_AspNetUserLogins PRIMARY KEY (LoginProvider, ProviderKey), CONSTRAINT FK_AspNetUserLogins_AspNetUsers_UserId FOREIGN KEY (UserId) REFERENCES dbo.AspNetUsers(Id) ON DELETE CASCADE);
IF OBJECT_ID(N'dbo.AspNetUserRoles', N'U') IS NULL
CREATE TABLE dbo.AspNetUserRoles (UserId NVARCHAR(450) NOT NULL, RoleId NVARCHAR(450) NOT NULL, CONSTRAINT PK_AspNetUserRoles PRIMARY KEY (UserId, RoleId), CONSTRAINT FK_AspNetUserRoles_AspNetRoles_RoleId FOREIGN KEY (RoleId) REFERENCES dbo.AspNetRoles(Id) ON DELETE CASCADE, CONSTRAINT FK_AspNetUserRoles_AspNetUsers_UserId FOREIGN KEY (UserId) REFERENCES dbo.AspNetUsers(Id) ON DELETE CASCADE);
IF OBJECT_ID(N'dbo.AspNetUserTokens', N'U') IS NULL
CREATE TABLE dbo.AspNetUserTokens (UserId NVARCHAR(450) NOT NULL, LoginProvider NVARCHAR(450) NOT NULL, Name NVARCHAR(450) NOT NULL, Value NVARCHAR(MAX) NULL, CONSTRAINT PK_AspNetUserTokens PRIMARY KEY (UserId, LoginProvider, Name), CONSTRAINT FK_AspNetUserTokens_AspNetUsers_UserId FOREIGN KEY (UserId) REFERENCES dbo.AspNetUsers(Id) ON DELETE CASCADE);
IF OBJECT_ID(N'dbo.AdminMenuPermissions', N'U') IS NULL
CREATE TABLE dbo.AdminMenuPermissions (Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AdminMenuPermissions PRIMARY KEY, RoleId NVARCHAR(450) NOT NULL, MenuKey NVARCHAR(100) NOT NULL, CONSTRAINT FK_AdminMenuPermissions_AspNetRoles_RoleId FOREIGN KEY (RoleId) REFERENCES dbo.AspNetRoles(Id) ON DELETE CASCADE);
IF OBJECT_ID(N'dbo.CustomerProfiles', N'U') IS NULL
CREATE TABLE dbo.CustomerProfiles (UserId NVARCHAR(450) NOT NULL CONSTRAINT PK_CustomerProfiles PRIMARY KEY, FullName NVARCHAR(MAX) NOT NULL, Phone NVARCHAR(512) NOT NULL, InvoiceType NVARCHAR(20) NOT NULL, InvoiceNit NVARCHAR(512) NULL, InvoiceName NVARCHAR(MAX) NULL, RecurrenteCustomerId NVARCHAR(128) NULL, CONSTRAINT FK_CustomerProfiles_AspNetUsers_UserId FOREIGN KEY (UserId) REFERENCES dbo.AspNetUsers(Id) ON DELETE CASCADE);
GO
IF COL_LENGTH(N'dbo.CustomerProfiles', N'RecurrenteCustomerId') IS NULL ALTER TABLE dbo.CustomerProfiles ADD RecurrenteCustomerId NVARCHAR(128) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'RoleNameIndex' AND object_id=OBJECT_ID(N'dbo.AspNetRoles')) CREATE UNIQUE INDEX RoleNameIndex ON dbo.AspNetRoles(NormalizedName) WHERE NormalizedName IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'EmailIndex' AND object_id=OBJECT_ID(N'dbo.AspNetUsers')) CREATE INDEX EmailIndex ON dbo.AspNetUsers(NormalizedEmail);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'UserNameIndex' AND object_id=OBJECT_ID(N'dbo.AspNetUsers')) CREATE UNIQUE INDEX UserNameIndex ON dbo.AspNetUsers(NormalizedUserName) WHERE NormalizedUserName IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_AspNetRoleClaims_RoleId' AND object_id=OBJECT_ID(N'dbo.AspNetRoleClaims')) CREATE INDEX IX_AspNetRoleClaims_RoleId ON dbo.AspNetRoleClaims(RoleId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_AspNetUserClaims_UserId' AND object_id=OBJECT_ID(N'dbo.AspNetUserClaims')) CREATE INDEX IX_AspNetUserClaims_UserId ON dbo.AspNetUserClaims(UserId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_AspNetUserLogins_UserId' AND object_id=OBJECT_ID(N'dbo.AspNetUserLogins')) CREATE INDEX IX_AspNetUserLogins_UserId ON dbo.AspNetUserLogins(UserId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_AspNetUserRoles_RoleId' AND object_id=OBJECT_ID(N'dbo.AspNetUserRoles')) CREATE INDEX IX_AspNetUserRoles_RoleId ON dbo.AspNetUserRoles(RoleId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'UX_AdminMenuPermissions_RoleId_MenuKey' AND object_id=OBJECT_ID(N'dbo.AdminMenuPermissions')) CREATE UNIQUE INDEX UX_AdminMenuPermissions_RoleId_MenuKey ON dbo.AdminMenuPermissions(RoleId, MenuKey);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_CustomerProfiles_RecurrenteCustomerId' AND object_id=OBJECT_ID(N'dbo.CustomerProfiles')) CREATE UNIQUE INDEX IX_CustomerProfiles_RecurrenteCustomerId ON dbo.CustomerProfiles(RecurrenteCustomerId) WHERE RecurrenteCustomerId IS NOT NULL;
GO
IF NOT EXISTS (SELECT 1 FROM dbo.AspNetRoles WHERE NormalizedName=N'SUPERADMIN') INSERT INTO dbo.AspNetRoles (Id,Name,NormalizedName,ConcurrencyStamp) VALUES (CONVERT(NVARCHAR(450),NEWID()),N'SuperAdmin',N'SUPERADMIN',CONVERT(NVARCHAR(450),NEWID()));
IF NOT EXISTS (SELECT 1 FROM dbo.AspNetRoles WHERE NormalizedName=N'ADMINISTRATOR') INSERT INTO dbo.AspNetRoles (Id,Name,NormalizedName,ConcurrencyStamp) VALUES (CONVERT(NVARCHAR(450),NEWID()),N'Administrator',N'ADMINISTRATOR',CONVERT(NVARCHAR(450),NEWID()));
IF NOT EXISTS (SELECT 1 FROM dbo.AspNetRoles WHERE NormalizedName=N'CUSTOMER') INSERT INTO dbo.AspNetRoles (Id,Name,NormalizedName,ConcurrencyStamp) VALUES (CONVERT(NVARCHAR(450),NEWID()),N'Customer',N'CUSTOMER',CONVERT(NVARCHAR(450),NEWID()));
GO
DECLARE @administratorRoleId NVARCHAR(450) = (SELECT Id FROM dbo.AspNetRoles WHERE NormalizedName=N'ADMINISTRATOR');
IF @administratorRoleId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.AdminMenuPermissions WHERE RoleId=@administratorRoleId AND MenuKey=N'dashboard') INSERT INTO dbo.AdminMenuPermissions (RoleId,MenuKey) VALUES (@administratorRoleId,N'dashboard');
IF @administratorRoleId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.AdminMenuPermissions WHERE RoleId=@administratorRoleId AND MenuKey=N'events') INSERT INTO dbo.AdminMenuPermissions (RoleId,MenuKey) VALUES (@administratorRoleId,N'events');
IF @administratorRoleId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.AdminMenuPermissions WHERE RoleId=@administratorRoleId AND MenuKey=N'email-template') INSERT INTO dbo.AdminMenuPermissions (RoleId,MenuKey) VALUES (@administratorRoleId,N'email-template');
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_Orders_AspNetUsers_CustomerUserId')
    ALTER TABLE dbo.Orders ADD CONSTRAINT FK_Orders_AspNetUsers_CustomerUserId FOREIGN KEY (CustomerUserId) REFERENCES dbo.AspNetUsers(Id) ON DELETE SET NULL;
GO
