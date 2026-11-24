-- Fase 3. Actualizacion aditiva para bases existentes. Ejecutar despues de 003 y 004.
SET XACT_ABORT ON;
GO
IF COL_LENGTH(N'dbo.Promotions',N'MaxUsesPerUser') IS NULL
 ALTER TABLE dbo.Promotions ADD MaxUsesPerUser int NULL;
GO
IF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE name=N'CK_Promotions_UserUses')
 ALTER TABLE dbo.Promotions ADD CONSTRAINT CK_Promotions_UserUses CHECK(MaxUsesPerUser IS NULL OR MaxUsesPerUser>0);
GO
-- La comparacion de identificadores de proveedor debe respetar mayusculas/minusculas.
IF EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.Payments') AND name=N'Proveedor' AND collation_name<>N'Latin1_General_100_BIN2')
BEGIN
 BEGIN TRANSACTION;
 DROP INDEX IX_Payments_Proveedor_TransaccionIdExterna ON dbo.Payments;
 ALTER TABLE dbo.Payments DROP CONSTRAINT CK_Payments_External, CK_Payments_Confirmed;
 ALTER TABLE dbo.Payments ALTER COLUMN Proveedor nvarchar(80) COLLATE Latin1_General_100_BIN2 NULL;
 ALTER TABLE dbo.Payments ALTER COLUMN TransaccionIdExterna nvarchar(160) COLLATE Latin1_General_100_BIN2 NULL;
 ALTER TABLE dbo.Payments WITH CHECK ADD CONSTRAINT CK_Payments_External CHECK (([TransaccionIdExterna] IS NULL AND [Proveedor] IS NULL) OR ([Metodo]=2 AND [TransaccionIdExterna] IS NOT NULL AND [Proveedor] IS NOT NULL));
 ALTER TABLE dbo.Payments WITH CHECK ADD CONSTRAINT CK_Payments_Confirmed CHECK ([Estado]<>2 OR ([ConfirmadoUtc] IS NOT NULL AND (([Metodo]=2 AND [TransaccionIdExterna] IS NOT NULL) OR ([Metodo]<>2 AND [ConfirmadoPorUsuarioId] IS NOT NULL))));
 CREATE UNIQUE INDEX IX_Payments_Proveedor_TransaccionIdExterna ON dbo.Payments(Proveedor,TransaccionIdExterna) WHERE TransaccionIdExterna IS NOT NULL;
 COMMIT;
END;
GO
IF OBJECT_ID(N'dbo.UsuarioFavoritos',N'U') IS NULL
BEGIN
CREATE TABLE [dbo].[UsuarioFavoritos] (
    [Id] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [Version] rowversion NOT NULL,
    [UsuarioId] uniqueidentifier NOT NULL,
    [ProductoId] uniqueidentifier NOT NULL,
    [CreadoUtc] datetimeoffset NOT NULL,
    CONSTRAINT [PK_UsuarioFavoritos] PRIMARY KEY ([Id]),
    CONSTRAINT [AK_UsuarioFavoritos_TenantId_Id] UNIQUE ([TenantId], [Id]),
    CONSTRAINT [FK_UsuarioFavoritos_Products_TenantId_ProductoId] FOREIGN KEY ([TenantId], [ProductoId]) REFERENCES [dbo].[Products] ([TenantId], [Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_UsuarioFavoritos_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_UsuarioFavoritos_Users_TenantId_UsuarioId] FOREIGN KEY ([TenantId], [UsuarioId]) REFERENCES [dbo].[Users] ([TenantId], [Id]) ON DELETE NO ACTION
);
END;
GO
IF OBJECT_ID(N'dbo.PaymentLogs',N'U') IS NULL
BEGIN
CREATE TABLE [dbo].[PaymentLogs] (
    [Id] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [Version] rowversion NOT NULL,
    [PaymentId] uniqueidentifier NOT NULL,
    [Proveedor] nvarchar(80) COLLATE Latin1_General_100_BIN2 NOT NULL,
    [EventoId] nvarchar(160) COLLATE Latin1_General_100_BIN2 NOT NULL,
    [TransaccionIdExterna] nvarchar(160) NOT NULL,
    [PayloadHash] char(64) NOT NULL,
    [EstadoExterno] nvarchar(20) NOT NULL,
    [Importe] decimal(18,2) NOT NULL,
    [Moneda] nvarchar(3) NOT NULL,
    [Resultado] nvarchar(40) NOT NULL,
    [RecibidoUtc] datetimeoffset NOT NULL,
    CONSTRAINT [PK_PaymentLogs] PRIMARY KEY ([Id]),
    CONSTRAINT [AK_PaymentLogs_TenantId_Id] UNIQUE ([TenantId], [Id]),
    CONSTRAINT [CK_PaymentLogs_Amounts] CHECK ([Importe]>=0 AND LEN([Moneda])=3),
    CONSTRAINT [FK_PaymentLogs_Payments_TenantId_PaymentId] FOREIGN KEY ([TenantId], [PaymentId]) REFERENCES [dbo].[Payments] ([TenantId], [Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_PaymentLogs_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION
);
END;
GO
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name=N'IX_Orders_TenantId_PromotionId_GuestEmailNormalized_EstadoPedido' AND object_id=OBJECT_ID(N'dbo.Orders'))
CREATE INDEX [IX_Orders_TenantId_PromotionId_GuestEmailNormalized_EstadoPedido] ON [dbo].[Orders] ([TenantId], [PromotionId], [GuestEmailNormalized], [EstadoPedido]);
GO
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name=N'IX_Orders_TenantId_PromotionId_UsuarioId_EstadoPedido' AND object_id=OBJECT_ID(N'dbo.Orders'))
CREATE INDEX [IX_Orders_TenantId_PromotionId_UsuarioId_EstadoPedido] ON [dbo].[Orders] ([TenantId], [PromotionId], [UsuarioId], [EstadoPedido]);
GO
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name=N'IX_PaymentLogs_Proveedor_EventoId' AND object_id=OBJECT_ID(N'dbo.PaymentLogs'))
CREATE UNIQUE INDEX [IX_PaymentLogs_Proveedor_EventoId] ON [dbo].[PaymentLogs] ([Proveedor], [EventoId]);
GO
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name=N'IX_PaymentLogs_TenantId_PaymentId_RecibidoUtc' AND object_id=OBJECT_ID(N'dbo.PaymentLogs'))
CREATE INDEX [IX_PaymentLogs_TenantId_PaymentId_RecibidoUtc] ON [dbo].[PaymentLogs] ([TenantId], [PaymentId], [RecibidoUtc]);
GO
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name=N'IX_UsuarioFavoritos_TenantId_ProductoId' AND object_id=OBJECT_ID(N'dbo.UsuarioFavoritos'))
CREATE INDEX [IX_UsuarioFavoritos_TenantId_ProductoId] ON [dbo].[UsuarioFavoritos] ([TenantId], [ProductoId]);
GO
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name=N'IX_UsuarioFavoritos_TenantId_UsuarioId_ProductoId' AND object_id=OBJECT_ID(N'dbo.UsuarioFavoritos'))
CREATE UNIQUE INDEX [IX_UsuarioFavoritos_TenantId_UsuarioId_ProductoId] ON [dbo].[UsuarioFavoritos] ([TenantId], [UsuarioId], [ProductoId]);
GO
CREATE OR ALTER TRIGGER [dbo].[TR_PaymentLogs_Immutable] ON [dbo].[PaymentLogs]
INSTEAD OF UPDATE, DELETE AS
BEGIN
 SET NOCOUNT ON;
 THROW 51003, 'La auditoria de pagos es inmutable.', 1;
END;
GO
