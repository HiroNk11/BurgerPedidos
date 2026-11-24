-- Actualización aditiva. Ejecutar después de 005; no recalcula pedidos ni pagos.
SET XACT_ABORT ON;
GO
IF COL_LENGTH(N'dbo.Payments',N'PreferenceId') IS NULL
 ALTER TABLE dbo.Payments ADD PreferenceId nvarchar(160) COLLATE Latin1_General_100_BIN2 NULL;
IF COL_LENGTH(N'dbo.Payments',N'CheckoutUrl') IS NULL
 ALTER TABLE dbo.Payments ADD CheckoutUrl nvarchar(2048) NULL;
GO
BEGIN TRANSACTION;
IF EXISTS(SELECT 1 FROM sys.check_constraints WHERE name=N'CK_Payments_External')
 ALTER TABLE dbo.Payments DROP CONSTRAINT CK_Payments_External;
ALTER TABLE dbo.Payments WITH CHECK ADD CONSTRAINT CK_Payments_External CHECK (
 ([TransaccionIdExterna] IS NULL AND [Proveedor] IS NULL AND [PreferenceId] IS NULL AND [CheckoutUrl] IS NULL)
 OR ([Metodo]=2 AND [Proveedor] IS NOT NULL AND ([TransaccionIdExterna] IS NOT NULL OR [PreferenceId] IS NOT NULL)));
IF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE name=N'CK_Payments_Preference')
 ALTER TABLE dbo.Payments WITH CHECK ADD CONSTRAINT CK_Payments_Preference CHECK (
 ([PreferenceId] IS NULL AND [CheckoutUrl] IS NULL) OR ([Metodo]=2 AND [PreferenceId] IS NOT NULL AND [CheckoutUrl] LIKE 'https://%'));
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.Payments') AND name=N'IX_Payments_Proveedor_PreferenceId')
 CREATE UNIQUE INDEX IX_Payments_Proveedor_PreferenceId ON dbo.Payments(Proveedor,PreferenceId) WHERE PreferenceId IS NOT NULL;
COMMIT;
GO
