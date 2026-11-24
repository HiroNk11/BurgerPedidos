-- Generado por EF Core 10 desde GastronomiaDbContext. Ejecutar en una base NUEVA.
CREATE SEQUENCE [dbo].[OrderCodeSequence] START WITH 1000 INCREMENT BY 1 NO CYCLE;
GO


CREATE TABLE [dbo].[Tenants] (
    [Id] uniqueidentifier NOT NULL,
    [Nombre] nvarchar(120) NOT NULL,
    [TimeZoneId] nvarchar(80) NOT NULL,
    [WhatsApp] nvarchar(15) NOT NULL,
    [Moneda] nvarchar(3) NOT NULL,
    [TarifaEnvio] decimal(18,2) NOT NULL,
    [Activo] bit NOT NULL,
    CONSTRAINT [PK_Tenants] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_Tenants_Currency] CHECK (LEN([Moneda]) = 3),
    CONSTRAINT [CK_Tenants_WhatsApp] CHECK ([WhatsApp] NOT LIKE '%[^0-9]%' AND LEN([WhatsApp]) BETWEEN 10 AND 15)
);
GO


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
GO


CREATE TABLE [dbo].[Products] (
    [Id] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [Version] rowversion NOT NULL,
    [Nombre] nvarchar(160) NOT NULL,
    [Precio] decimal(18,2) NOT NULL,
    [Stock] int NOT NULL,
    [Categoria] nvarchar(40) NOT NULL,
    [Descripcion] nvarchar(500) NOT NULL,
    [ImagenKey] nvarchar(40) NULL,
    [Activo] bit NOT NULL,
    CONSTRAINT [PK_Products] PRIMARY KEY ([Id]),
    CONSTRAINT [AK_Products_TenantId_Id] UNIQUE ([TenantId], [Id]),
    CONSTRAINT [CK_Products_Price] CHECK ([Precio]>=0),
    CONSTRAINT [CK_Products_Stock] CHECK ([Stock]>=0),
    CONSTRAINT [FK_Products_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [dbo].[Promotions] (
    [Id] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [Version] rowversion NOT NULL,
    [Nombre] nvarchar(120) NOT NULL,
    [Tipo] int NOT NULL,
    [TipoDescuento] int NOT NULL,
    [Valor] decimal(18,2) NOT NULL,
    [MinimoCompra] decimal(18,2) NOT NULL,
    [CouponCode] nvarchar(40) COLLATE Latin1_General_100_BIN2 NULL,
    [DesdeUtc] datetimeoffset NOT NULL,
    [HastaUtc] datetimeoffset NOT NULL,
    [DiasSemanaMask] int NULL,
    [HoraInicio] time(0) NULL,
    [HoraFin] time(0) NULL,
    [MaxUses] int NULL,
    [MaxUsesPerUser] int NULL,
    [CurrentUses] int NOT NULL,
    [Activa] bit NOT NULL,
    CONSTRAINT [PK_Promotions] PRIMARY KEY ([Id]),
    CONSTRAINT [AK_Promotions_TenantId_Id] UNIQUE ([TenantId], [Id]),
    CONSTRAINT [CK_Promotions_Coupon] CHECK ([CouponCode] IS NULL OR (LEN([CouponCode])>0 AND [CouponCode]=UPPER(LTRIM(RTRIM([CouponCode]))))),
    CONSTRAINT [CK_Promotions_Dates] CHECK ([HastaUtc]>[DesdeUtc]),
    CONSTRAINT [CK_Promotions_Enums] CHECK ([Tipo] BETWEEN 0 AND 3 AND [TipoDescuento] BETWEEN 0 AND 1),
    CONSTRAINT [CK_Promotions_Schedule] CHECK (([DiasSemanaMask] IS NULL OR [DiasSemanaMask] BETWEEN 1 AND 127) AND (([HoraInicio] IS NULL AND [HoraFin] IS NULL) OR ([HoraInicio] IS NOT NULL AND [HoraFin] IS NOT NULL AND [HoraInicio]<>[HoraFin])) AND ([Tipo]<>1 OR [DiasSemanaMask] IS NOT NULL) AND ([Tipo]<>2 OR [HoraInicio] IS NOT NULL) AND ([Tipo]<>3 OR [CouponCode] IS NOT NULL)),
    CONSTRAINT [CK_Promotions_UserUses] CHECK ([MaxUsesPerUser] IS NULL OR [MaxUsesPerUser]>0),
    CONSTRAINT [CK_Promotions_Uses] CHECK ([CurrentUses]>=0 AND ([MaxUses] IS NULL OR ([MaxUses]>0 AND [CurrentUses]<=[MaxUses]))),
    CONSTRAINT [CK_Promotions_Value] CHECK ([Valor]>0 AND ([TipoDescuento]<>0 OR [Valor]<=100) AND [MinimoCompra]>=0),
    CONSTRAINT [FK_Promotions_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [dbo].[Users] (
    [Id] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [Version] rowversion NOT NULL,
    [Email] nvarchar(254) NOT NULL,
    [NormalizedEmail] nvarchar(254) NOT NULL,
    [EmailVerificado] bit NOT NULL,
    [Nombre] nvarchar(100) NOT NULL,
    [Telefono] nvarchar(25) NOT NULL,
    CONSTRAINT [PK_Users] PRIMARY KEY ([TenantId], [Id]),
    CONSTRAINT [FK_Users_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION
);
GO


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
GO


CREATE TABLE [dbo].[Orders] (
    [Id] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [Version] rowversion NOT NULL,
    [ShortCode] nvarchar(32) NOT NULL DEFAULT (('#PED-' + CONVERT(varchar(20), NEXT VALUE FOR [dbo].[OrderCodeSequence]))),
    [CheckoutTokenHash] nvarchar(64) NULL,
    [NombreCliente] nvarchar(100) NOT NULL,
    [TipoEntrega] int NOT NULL,
    [DireccionEntrega] nvarchar(400) NULL,
    [Latitud] float NULL,
    [Longitud] float NULL,
    [Notas] nvarchar(500) NULL,
    [ImporteEnvio] decimal(18,2) NOT NULL,
    [RequestId] uniqueidentifier NOT NULL,
    [RequestHash] char(64) NOT NULL,
    [UsuarioId] uniqueidentifier NULL,
    [GuestEmail] nvarchar(254) NOT NULL,
    [GuestEmailNormalized] nvarchar(254) NOT NULL,
    [GuestPhone] nvarchar(25) NOT NULL,
    [CreadoUtc] datetimeoffset NOT NULL,
    [EstadoPedido] int NOT NULL,
    [EstadoPago] int NOT NULL,
    [ImporteBruto] decimal(18,2) NOT NULL,
    [DescuentoTotal] decimal(18,2) NOT NULL,
    [Total] decimal(18,2) NOT NULL,
    [Moneda] nvarchar(3) NOT NULL,
    [PromotionId] uniqueidentifier NULL,
    [PromocionHistorica] nvarchar(120) NULL,
    CONSTRAINT [PK_Orders] PRIMARY KEY ([Id]),
    CONSTRAINT [AK_Orders_TenantId_Id] UNIQUE ([TenantId], [Id]),
    CONSTRAINT [CK_Orders_Code] CHECK ([ShortCode] LIKE '#PED-%'),
    CONSTRAINT [CK_Orders_Delivery] CHECK ([TipoEntrega] IN (0,1) AND ([TipoEntrega]=0 OR ([DireccionEntrega] IS NOT NULL AND LEN(LTRIM(RTRIM([DireccionEntrega])))>0)) AND (([Latitud] IS NULL AND [Longitud] IS NULL) OR ([Latitud] IS NOT NULL AND [Longitud] IS NOT NULL AND [Latitud] BETWEEN -90 AND 90 AND [Longitud] BETWEEN -180 AND 180))),
    CONSTRAINT [CK_Orders_States] CHECK ([EstadoPedido] BETWEEN 0 AND 5 AND [EstadoPago] BETWEEN 0 AND 3),
    CONSTRAINT [CK_Orders_Totals] CHECK ([ImporteBruto]>=0 AND [DescuentoTotal]>=0 AND [DescuentoTotal]<=[ImporteBruto] AND [ImporteEnvio]>=0 AND [Total]=[ImporteBruto]-[DescuentoTotal]+[ImporteEnvio]),
    CONSTRAINT [FK_Orders_Promotions_TenantId_PromotionId] FOREIGN KEY ([TenantId], [PromotionId]) REFERENCES [dbo].[Promotions] ([TenantId], [Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Orders_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Orders_Users_TenantId_UsuarioId] FOREIGN KEY ([TenantId], [UsuarioId]) REFERENCES [dbo].[Users] ([TenantId], [Id]) ON DELETE NO ACTION
);
GO


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
GO


CREATE TABLE [dbo].[OrderItems] (
    [Id] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [Version] rowversion NOT NULL,
    [OrderId] uniqueidentifier NOT NULL,
    [ProductId] uniqueidentifier NOT NULL,
    [NombreProductoHistorico] nvarchar(160) NOT NULL,
    [PrecioUnitarioHistorico] decimal(18,2) NOT NULL,
    [DescuentoAplicado] decimal(18,2) NOT NULL,
    [Cantidad] int NOT NULL,
    [Subtotal] decimal(18,2) NOT NULL,
    CONSTRAINT [PK_OrderItems] PRIMARY KEY ([Id]),
    CONSTRAINT [AK_OrderItems_TenantId_Id] UNIQUE ([TenantId], [Id]),
    CONSTRAINT [CK_OrderItems_Amounts] CHECK ([Cantidad] BETWEEN 1 AND 1000 AND [PrecioUnitarioHistorico]>=0 AND [DescuentoAplicado]>=0 AND [DescuentoAplicado]<=[PrecioUnitarioHistorico]*[Cantidad] AND [Subtotal]=[PrecioUnitarioHistorico]*[Cantidad]-[DescuentoAplicado]),
    CONSTRAINT [FK_OrderItems_Orders_TenantId_OrderId] FOREIGN KEY ([TenantId], [OrderId]) REFERENCES [dbo].[Orders] ([TenantId], [Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_OrderItems_Products_TenantId_ProductId] FOREIGN KEY ([TenantId], [ProductId]) REFERENCES [dbo].[Products] ([TenantId], [Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_OrderItems_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [dbo].[OutboxMessages] (
    [Id] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [Version] rowversion NOT NULL,
    [OrderId] uniqueidentifier NOT NULL,
    [Tipo] nvarchar(80) NOT NULL,
    [CreadoUtc] datetimeoffset NOT NULL,
    [ProcesadoUtc] datetimeoffset NULL,
    [Attempts] int NOT NULL,
    [NextAttemptUtc] datetimeoffset NULL,
    [LockedUntilUtc] datetimeoffset NULL,
    [LeaseId] uniqueidentifier NULL,
    [LastError] nvarchar(1000) NULL,
    [ProviderMessageId] nvarchar(160) NULL,
    CONSTRAINT [PK_OutboxMessages] PRIMARY KEY ([Id]),
    CONSTRAINT [AK_OutboxMessages_TenantId_Id] UNIQUE ([TenantId], [Id]),
    CONSTRAINT [FK_OutboxMessages_Orders_TenantId_OrderId] FOREIGN KEY ([TenantId], [OrderId]) REFERENCES [dbo].[Orders] ([TenantId], [Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_OutboxMessages_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [dbo].[Payments] (
    [Id] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [Version] rowversion NOT NULL,
    [OrderId] uniqueidentifier NOT NULL,
    [Metodo] int NOT NULL,
    [Estado] int NOT NULL,
    [Importe] decimal(18,2) NOT NULL,
    [Moneda] nvarchar(3) NOT NULL,
    [ComprobanteUrl] nvarchar(2048) NULL,
    [Proveedor] nvarchar(80) COLLATE Latin1_General_100_BIN2 NULL,
    [TransaccionIdExterna] nvarchar(160) COLLATE Latin1_General_100_BIN2 NULL,
    [PreferenceId] nvarchar(160) COLLATE Latin1_General_100_BIN2 NULL,
    [CheckoutUrl] nvarchar(2048) NULL,
    [CreadoUtc] datetimeoffset NOT NULL,
    [ConfirmadoUtc] datetimeoffset NULL,
    [ConfirmadoPorUsuarioId] uniqueidentifier NULL,
    CONSTRAINT [PK_Payments] PRIMARY KEY ([Id]),
    CONSTRAINT [AK_Payments_TenantId_Id] UNIQUE ([TenantId], [Id]),
    CONSTRAINT [CK_Payments_Amounts] CHECK ([Importe]>=0 AND [Metodo] BETWEEN 0 AND 2 AND [Estado] BETWEEN 0 AND 3),
    CONSTRAINT [CK_Payments_Confirmed] CHECK ([Estado]<>2 OR ([ConfirmadoUtc] IS NOT NULL AND (([Metodo]=2 AND [TransaccionIdExterna] IS NOT NULL) OR ([Metodo]<>2 AND [ConfirmadoPorUsuarioId] IS NOT NULL)))),
    CONSTRAINT [CK_Payments_External] CHECK (([TransaccionIdExterna] IS NULL AND [Proveedor] IS NULL AND [PreferenceId] IS NULL AND [CheckoutUrl] IS NULL) OR ([Metodo]=2 AND [Proveedor] IS NOT NULL AND ([TransaccionIdExterna] IS NOT NULL OR [PreferenceId] IS NOT NULL))),
    CONSTRAINT [CK_Payments_Preference] CHECK (([PreferenceId] IS NULL AND [CheckoutUrl] IS NULL) OR ([Metodo]=2 AND [PreferenceId] IS NOT NULL AND [CheckoutUrl] LIKE 'https://%')),
    CONSTRAINT [CK_Payments_Receipt] CHECK (([ComprobanteUrl] IS NULL OR ([Metodo]=1 AND ([ComprobanteUrl] LIKE 'https://%' OR [ComprobanteUrl] LIKE '/api/tenants/%/staff/receipts/%'))) AND ([Estado]<>1 OR [ComprobanteUrl] IS NOT NULL)),
    CONSTRAINT [FK_Payments_Orders_TenantId_OrderId] FOREIGN KEY ([TenantId], [OrderId]) REFERENCES [dbo].[Orders] ([TenantId], [Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Payments_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Payments_Users_TenantId_ConfirmadoPorUsuarioId] FOREIGN KEY ([TenantId], [ConfirmadoPorUsuarioId]) REFERENCES [dbo].[Users] ([TenantId], [Id]) ON DELETE NO ACTION
);
GO


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
GO


CREATE INDEX [IX_Direcciones_TenantId_UsuarioId] ON [dbo].[Direcciones] ([TenantId], [UsuarioId]);
GO


CREATE UNIQUE INDEX [IX_OrderItems_TenantId_OrderId_ProductId] ON [dbo].[OrderItems] ([TenantId], [OrderId], [ProductId]);
GO


CREATE INDEX [IX_OrderItems_TenantId_ProductId] ON [dbo].[OrderItems] ([TenantId], [ProductId]);
GO


CREATE UNIQUE INDEX [IX_Orders_ShortCode] ON [dbo].[Orders] ([ShortCode]);
GO


CREATE INDEX [IX_Orders_TenantId_EstadoPedido_CreadoUtc] ON [dbo].[Orders] ([TenantId], [EstadoPedido], [CreadoUtc]);
GO


CREATE INDEX [IX_Orders_TenantId_GuestEmailNormalized] ON [dbo].[Orders] ([TenantId], [GuestEmailNormalized]) WHERE [UsuarioId] IS NULL;
GO


CREATE INDEX [IX_Orders_TenantId_PromotionId_GuestEmailNormalized_EstadoPedido] ON [dbo].[Orders] ([TenantId], [PromotionId], [GuestEmailNormalized], [EstadoPedido]);
GO


CREATE INDEX [IX_Orders_TenantId_PromotionId_UsuarioId_EstadoPedido] ON [dbo].[Orders] ([TenantId], [PromotionId], [UsuarioId], [EstadoPedido]);
GO


CREATE UNIQUE INDEX [IX_Orders_TenantId_RequestId] ON [dbo].[Orders] ([TenantId], [RequestId]);
GO


CREATE INDEX [IX_Orders_TenantId_UsuarioId_CreadoUtc] ON [dbo].[Orders] ([TenantId], [UsuarioId], [CreadoUtc]);
GO


CREATE INDEX [IX_OtpChallenges_ExpiraUtc] ON [dbo].[OtpChallenges] ([ExpiraUtc]);
GO


CREATE UNIQUE INDEX [IX_OtpChallenges_TenantId_NormalizedEmail] ON [dbo].[OtpChallenges] ([TenantId], [NormalizedEmail]);
GO


CREATE INDEX [IX_OutboxMessages_TenantId_CreadoUtc] ON [dbo].[OutboxMessages] ([TenantId], [CreadoUtc]) WHERE [ProcesadoUtc] IS NULL;
GO


CREATE UNIQUE INDEX [IX_OutboxMessages_TenantId_OrderId_Tipo] ON [dbo].[OutboxMessages] ([TenantId], [OrderId], [Tipo]);
GO


CREATE UNIQUE INDEX [IX_PaymentLogs_Proveedor_EventoId] ON [dbo].[PaymentLogs] ([Proveedor], [EventoId]);
GO


CREATE INDEX [IX_PaymentLogs_TenantId_PaymentId_RecibidoUtc] ON [dbo].[PaymentLogs] ([TenantId], [PaymentId], [RecibidoUtc]);
GO


CREATE UNIQUE INDEX [IX_Payments_Proveedor_PreferenceId] ON [dbo].[Payments] ([Proveedor], [PreferenceId]) WHERE [PreferenceId] IS NOT NULL;
GO


CREATE UNIQUE INDEX [IX_Payments_Proveedor_TransaccionIdExterna] ON [dbo].[Payments] ([Proveedor], [TransaccionIdExterna]) WHERE [TransaccionIdExterna] IS NOT NULL;
GO


CREATE INDEX [IX_Payments_TenantId_ConfirmadoPorUsuarioId] ON [dbo].[Payments] ([TenantId], [ConfirmadoPorUsuarioId]);
GO


CREATE UNIQUE INDEX [IX_Payments_TenantId_OrderId] ON [dbo].[Payments] ([TenantId], [OrderId]) WHERE [Estado] = 2;
GO


CREATE INDEX [IX_Products_TenantId_Activo] ON [dbo].[Products] ([TenantId], [Activo]);
GO


CREATE INDEX [IX_Promotions_TenantId_Activa_DesdeUtc_HastaUtc] ON [dbo].[Promotions] ([TenantId], [Activa], [DesdeUtc], [HastaUtc]);
GO


CREATE UNIQUE INDEX [IX_Promotions_TenantId_CouponCode] ON [dbo].[Promotions] ([TenantId], [CouponCode]) WHERE [CouponCode] IS NOT NULL;
GO


CREATE UNIQUE INDEX [IX_Users_TenantId_NormalizedEmail] ON [dbo].[Users] ([TenantId], [NormalizedEmail]);
GO


CREATE INDEX [IX_UsuarioFavoritos_TenantId_ProductoId] ON [dbo].[UsuarioFavoritos] ([TenantId], [ProductoId]);
GO


CREATE UNIQUE INDEX [IX_UsuarioFavoritos_TenantId_UsuarioId_ProductoId] ON [dbo].[UsuarioFavoritos] ([TenantId], [UsuarioId], [ProductoId]);
GO


