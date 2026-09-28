using Microsoft.EntityFrameworkCore;

namespace TicketSales.Data;

public static class AdminIdentitySchema
{
    public static async Task EnsureCreatedAsync(AdminIdentityDbContext db, CancellationToken cancellationToken = default)
    {
        await db.Database.ExecuteSqlRawAsync("""
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
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'RoleNameIndex' AND object_id=OBJECT_ID(N'dbo.AspNetRoles')) CREATE UNIQUE INDEX RoleNameIndex ON dbo.AspNetRoles(NormalizedName) WHERE NormalizedName IS NOT NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'EmailIndex' AND object_id=OBJECT_ID(N'dbo.AspNetUsers')) CREATE INDEX EmailIndex ON dbo.AspNetUsers(NormalizedEmail);
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'UserNameIndex' AND object_id=OBJECT_ID(N'dbo.AspNetUsers')) CREATE UNIQUE INDEX UserNameIndex ON dbo.AspNetUsers(NormalizedUserName) WHERE NormalizedUserName IS NOT NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_AspNetRoleClaims_RoleId' AND object_id=OBJECT_ID(N'dbo.AspNetRoleClaims')) CREATE INDEX IX_AspNetRoleClaims_RoleId ON dbo.AspNetRoleClaims(RoleId);
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_AspNetUserClaims_UserId' AND object_id=OBJECT_ID(N'dbo.AspNetUserClaims')) CREATE INDEX IX_AspNetUserClaims_UserId ON dbo.AspNetUserClaims(UserId);
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_AspNetUserLogins_UserId' AND object_id=OBJECT_ID(N'dbo.AspNetUserLogins')) CREATE INDEX IX_AspNetUserLogins_UserId ON dbo.AspNetUserLogins(UserId);
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_AspNetUserRoles_RoleId' AND object_id=OBJECT_ID(N'dbo.AspNetUserRoles')) CREATE INDEX IX_AspNetUserRoles_RoleId ON dbo.AspNetUserRoles(RoleId);
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'UX_AdminMenuPermissions_RoleId_MenuKey' AND object_id=OBJECT_ID(N'dbo.AdminMenuPermissions')) CREATE UNIQUE INDEX UX_AdminMenuPermissions_RoleId_MenuKey ON dbo.AdminMenuPermissions(RoleId, MenuKey);
            """, cancellationToken);

        // Execute this separately so SQL Server recognizes the new column before validating its index.
        await db.Database.ExecuteSqlRawAsync("""
            IF COL_LENGTH(N'dbo.CustomerProfiles', N'RecurrenteCustomerId') IS NULL
                ALTER TABLE dbo.CustomerProfiles ADD RecurrenteCustomerId NVARCHAR(128) NULL;
            """, cancellationToken);

        await db.Database.ExecuteSqlRawAsync("""
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_CustomerProfiles_RecurrenteCustomerId' AND object_id=OBJECT_ID(N'dbo.CustomerProfiles'))
                CREATE UNIQUE INDEX IX_CustomerProfiles_RecurrenteCustomerId ON dbo.CustomerProfiles(RecurrenteCustomerId) WHERE RecurrenteCustomerId IS NOT NULL;
            """, cancellationToken);
    }
}
