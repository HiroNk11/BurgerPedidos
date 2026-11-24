-- Fase 2: actualización aditiva para una base existente. No borra pedidos.
SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF COL_LENGTH('dbo.Users','Nombre') IS NULL
 ALTER TABLE dbo.Users ADD Nombre nvarchar(100) NOT NULL CONSTRAINT DF_Users_Nombre DEFAULT N'';
IF COL_LENGTH('dbo.Users','Telefono') IS NULL
 ALTER TABLE dbo.Users ADD Telefono nvarchar(25) NOT NULL CONSTRAINT DF_Users_Telefono DEFAULT N'';
IF OBJECT_ID(N'dbo.OtpChallenges', N'U') IS NULL
BEGIN
CREATE TABLE [dbo].[OtpChallenges] (
    [Id] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [Version] rowversion NOT NULL,
    [NormalizedEmail] nvarchar(254) NOT NULL,
    [Email] nvarchar(254) NOT NULL,
    [CodeHash] binary(32) NOT NULL,
    [MessageId] uniqueidentifier NOT NULL,
    [CreadoUtc] datetimeoffset NOT NULL,
    [ExpiraUtc] datetimeoffset NOT NULL,
    [ConsumidoUtc] datetimeoffset NULL,
    [VentanaUtc] datetimeoffset NOT NULL,
    [EnviosEnVentana] int NOT NULL,
    [IntentosFallidos] int NOT NULL,
    CONSTRAINT [PK_OtpChallenges] PRIMARY KEY ([Id]),
    CONSTRAINT [AK_OtpChallenges_TenantId_Id] UNIQUE ([TenantId], [Id]),
    CONSTRAINT [CK_OtpChallenges_Attempts] CHECK ([IntentosFallidos] BETWEEN 0 AND 5 AND [EnviosEnVentana] BETWEEN 1 AND 5),
    CONSTRAINT [CK_OtpChallenges_Expiration] CHECK ([ExpiraUtc]>[CreadoUtc]),
    CONSTRAINT [FK_OtpChallenges_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION
);
END;
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.OtpChallenges') AND name=N'IX_OtpChallenges_ExpiraUtc')
 CREATE INDEX [IX_OtpChallenges_ExpiraUtc] ON [dbo].[OtpChallenges] ([ExpiraUtc]);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.OtpChallenges') AND name=N'IX_OtpChallenges_TenantId_NormalizedEmail')
 CREATE UNIQUE INDEX [IX_OtpChallenges_TenantId_NormalizedEmail] ON [dbo].[OtpChallenges] ([TenantId], [NormalizedEmail]);
IF OBJECT_ID(N'dbo.Direcciones', N'U') IS NULL
BEGIN
CREATE TABLE [dbo].[Direcciones] (
    [Id] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [UsuarioId] uniqueidentifier NOT NULL,
    [Version] rowversion NOT NULL,
    [Calle] nvarchar(180) NOT NULL,
    [Numero] nvarchar(20) NOT NULL,
    [PisoDepto] nvarchar(60) NULL,
    [Latitud] float NULL,
    [Longitud] float NULL,
    CONSTRAINT [PK_Direcciones] PRIMARY KEY ([Id]),
    CONSTRAINT [AK_Direcciones_TenantId_Id] UNIQUE ([TenantId], [Id]),
    CONSTRAINT [CK_Direcciones_Coordinates] CHECK (([Latitud] IS NULL AND [Longitud] IS NULL) OR ([Latitud] IS NOT NULL AND [Longitud] IS NOT NULL AND [Latitud] BETWEEN -90 AND 90 AND [Longitud] BETWEEN -180 AND 180)),
    CONSTRAINT [FK_Direcciones_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Direcciones_Users_TenantId_UsuarioId] FOREIGN KEY ([TenantId], [UsuarioId]) REFERENCES [dbo].[Users] ([TenantId], [Id]) ON DELETE NO ACTION
);
END;
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.Direcciones') AND name=N'IX_Direcciones_TenantId_UsuarioId')
 CREATE INDEX [IX_Direcciones_TenantId_UsuarioId] ON [dbo].[Direcciones] ([TenantId], [UsuarioId]);
COMMIT TRANSACTION;
GO
