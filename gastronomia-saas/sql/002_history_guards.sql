-- Ejecutar después de 001_schema.sql en una base nueva.
-- Evita alterar precios/descuentos históricos incluso usando SQL directo.
CREATE OR ALTER TRIGGER [dbo].[TR_OrderItems_Immutable]
ON [dbo].[OrderItems] AFTER UPDATE, DELETE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS (SELECT 1 FROM deleted)
  THROW 51000, 'OrderItems históricos: UPDATE y DELETE no permitidos.', 1;
END;
GO
CREATE OR ALTER TRIGGER [dbo].[TR_Orders_Snapshot]
ON [dbo].[Orders] AFTER UPDATE, DELETE AS
BEGIN
 SET NOCOUNT ON;
 IF NOT EXISTS (SELECT 1 FROM inserted) AND EXISTS (SELECT 1 FROM deleted)
  THROW 51001, 'No se eliminan pedidos históricos.', 1;
 IF UPDATE(ImporteBruto) OR UPDATE(DescuentoTotal) OR UPDATE(Total) OR UPDATE(Moneda)
 OR UPDATE(ShortCode) OR UPDATE(GuestEmail) OR UPDATE(GuestEmailNormalized)
 OR UPDATE(GuestPhone) OR UPDATE(PromotionId) OR UPDATE(PromocionHistorica)
 OR UPDATE(CheckoutTokenHash) OR UPDATE(NombreCliente) OR UPDATE(TipoEntrega) OR UPDATE(DireccionEntrega) OR UPDATE(Latitud) OR UPDATE(Longitud) OR UPDATE(Notas) OR UPDATE(ImporteEnvio)
 OR UPDATE(CreadoUtc) OR UPDATE(RequestId) OR UPDATE(RequestHash) OR UPDATE(TenantId)
  THROW 51002, 'El snapshot del pedido es inmutable.', 1;
END;
GO

CREATE OR ALTER TRIGGER [dbo].[TR_PaymentLogs_Immutable] ON [dbo].[PaymentLogs]
INSTEAD OF UPDATE, DELETE AS
BEGIN
 SET NOCOUNT ON;
 THROW 51003, 'La auditoria de pagos es inmutable.', 1;
END;
GO
