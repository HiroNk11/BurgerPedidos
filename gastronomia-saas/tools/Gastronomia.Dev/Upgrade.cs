using System.Text.Json;
using System.Text.RegularExpressions;
using Gastronomia.Application;
using Gastronomia.Domain;
using Gastronomia.Infrastructure;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
internal static class Upgrade {
 public static async Task RunAsync(string root){
  root=Path.GetFullPath(root);var configPath=Path.Combine(root,"src/Gastronomia.Api/appsettings.Development.local.json");using var config=JsonDocument.Parse(await File.ReadAllTextAsync(configPath));
  string connection=config.RootElement.GetProperty("ConnectionStrings").GetProperty("SqlServer").GetString()!;var builder=new SqlConnectionStringBuilder(connection);
  if(!builder.InitialCatalog.StartsWith("BurgerClubPanelDev_",StringComparison.Ordinal))throw new InvalidOperationException("Este actualizador solo admite la base local de demostración creada por Gastronomia.Dev.");
  Guid tenantId=config.RootElement.GetProperty("Development").GetProperty("LocalStaff").GetProperty("TenantId").GetGuid();var scope=new TenantContext{TenantId=tenantId};var options=new DbContextOptionsBuilder<GastronomiaDbContext>().UseSqlServer(connection).Options;
  await using var db=new GastronomiaDbContext(options,scope);await db.Database.OpenConnectionAsync();
  foreach(var name in new[]{"003_checkout_notifications.sql","004_customer_accounts.sql","005_engagement_payments.sql","006_mercadopago_checkout.sql","002_history_guards.sql"}){string sql=await File.ReadAllTextAsync(Path.Combine(root,"sql",name));foreach(var batch in Regex.Split(sql,@"^\s*GO\s*$",RegexOptions.Multiline|RegexOptions.IgnoreCase))if(!string.IsNullOrWhiteSpace(batch)){using var command=db.Database.GetDbConnection().CreateCommand();command.CommandText=batch;await command.ExecuteNonQueryAsync();}}
  var products=JsonSerializer.Deserialize<List<CatalogItem>>(await File.ReadAllTextAsync(Path.Combine(root,"tools/catalog-demo.json")),new JsonSerializerOptions{PropertyNameCaseInsensitive=true})!;
  foreach(var p in products){var existing=await db.Products.SingleOrDefaultAsync(x=>x.Nombre==p.Nombre);if(existing is null){existing=new Product(tenantId,p.Nombre,p.Precio,100);db.Products.Add(existing);}existing.ActualizarPresentacion(p.Categoria,p.Descripcion,p.ImagenKey);}
  await db.SaveChangesAsync();Console.WriteLine("Actualización aplicada: pedidos y stock conservados; catálogo y campos de checkout preparados.");
 }
 private sealed record CatalogItem(string Nombre,string Descripcion,decimal Precio,string Categoria,string? ImagenKey);
}