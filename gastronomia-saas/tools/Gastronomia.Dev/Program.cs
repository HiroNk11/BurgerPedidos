using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Gastronomia.Application;
using Gastronomia.Domain;
using Gastronomia.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

if(args.Length==2&&args[0]=="--upgrade"){await Upgrade.RunAsync(args[1]);return;}
if(args.Length!=2) {Console.WriteLine("Uso: Gastronomia.Dev <directorio gastronomia-saas> <archivo de acceso local.md>");return;}
string root=Path.GetFullPath(args[0]);string access=Path.GetFullPath(args[1]);
string configPath=Path.Combine(root,"src","Gastronomia.Api","appsettings.Development.local.json");
if(File.Exists(configPath)) throw new InvalidOperationException("Ya hay una configuración local. No se sobrescribe ni se reinicializa la base existente.");
string password=Convert.ToBase64String(RandomNumberGenerator.GetBytes(24));
string email="admin@burgerclub.local";Guid userId=Guid.NewGuid();
string database="BurgerClubPanelDev_"+Guid.NewGuid().ToString("N")[..12];
var masterBuilder=new SqlConnectionStringBuilder("Server=(localdb)\\MSSQLLocalDB;Integrated Security=true;TrustServerCertificate=true"){InitialCatalog="master"};
await using(var master=new SqlConnection(masterBuilder.ConnectionString)) {await master.OpenAsync();await using var command=master.CreateCommand();command.CommandText=$"CREATE DATABASE [{database}]";await command.ExecuteNonQueryAsync();}
var connection=new SqlConnectionStringBuilder(masterBuilder.ConnectionString){InitialCatalog=database}.ConnectionString;
var options=new DbContextOptionsBuilder<GastronomiaDbContext>().UseSqlServer(connection).Options;
var tenant=new Tenant("Burger Club · demostración","5491124830571");var scope=new TenantContext{TenantId=tenant.Id};var factory=new DevFactory(options,scope);
await using(var db=await factory.CreateDbContextAsync()) {
 await db.Database.OpenConnectionAsync();
 foreach(var name in new[]{"001_schema.sql","002_history_guards.sql"}) {
  var sql=await File.ReadAllTextAsync(Path.Combine(root,"sql",name));
  foreach(var batch in Regex.Split(sql,@"^\s*GO\s*$",RegexOptions.Multiline|RegexOptions.IgnoreCase)) if(!string.IsNullOrWhiteSpace(batch)) {using var command=db.Database.GetDbConnection().CreateCommand();command.CommandText=batch;await command.ExecuteNonQueryAsync();}
 }
 db.Tenants.Add(tenant);db.Users.Add(new Usuario(tenant.Id,userId,email,true));await db.SaveChangesAsync();
}
var burger=new Product(tenant.Id,"La Clásica",8500,100);var bacon=new Product(tenant.Id,"Doble Bacon",11500,100);var fries=new Product(tenant.Id,"Papas cheddar",5200,100);var coke=new Product(tenant.Id,"Coca-Cola · lata 354 ml",2500,100);
await using(var db=await factory.CreateDbContextAsync()){db.AddRange(burger,bacon,fries,coke);db.Promotions.Add(new Promotion(tenant.Id,"Cupón Club 10%",TipoPromocion.Cupon,TipoDescuento.Porcentaje,10,DateTimeOffset.UtcNow.AddDays(-1),DateTimeOffset.UtcNow.AddDays(30),100,"CLUB10"));await db.SaveChangesAsync();}
var service=new OrderService(factory,scope,TimeProvider.System);
for(int i=0;i<6;i++) {
 var method=i==5?MetodoPago.PasarelaOnline:i%2==0?MetodoPago.TransferenciaCBU:MetodoPago.Efectivo;
 var order=await service.CrearPedidoAsync(new CrearPedidoRequest(Guid.NewGuid(),$"cliente{i+1}@example.com","5491100000000",method,[new(i%2==0?burger.Id:bacon.Id,1),new(i%2==0?fries.Id:coke.Id,1)],i%2==0?"CLUB10":null),null);
 if(i==1) await service.AceptarPedidoAsync(order.Id,true);
 if(i==2) {
  await using(var db=await factory.CreateDbContextAsync()) {var payment=await db.Payments.SingleAsync(p=>p.Id==order.PaymentId);payment.AdjuntarComprobante("https://placehold.co/900x600/png?text=COMPROBANTE+DEMO+SIN+VALIDEZ");var o=await db.Orders.SingleAsync(o=>o.Id==order.Id);o.RegistrarPago(EstadoPago.ComprobanteEnviado);await db.SaveChangesAsync();}
  await service.AceptarPedidoAsync(order.Id);
 }
 if(i==3) {await service.AceptarPedidoAsync(order.Id,true);await service.ConfirmarPagoManualAsync(order.Id,order.PaymentId,userId,order.Total);await service.CambiarEstadoAsync(order.Id,EstadoPedido.Entregado);}
 if(i==4) await service.CambiarEstadoAsync(order.Id,EstadoPedido.Cancelado);
}
var config=new {ConnectionStrings=new {SqlServer=connection},Development=new {LocalStaff=new {Enabled=true,TenantId=tenant.Id,UserId=userId,Email=email,PasswordHash=new PasswordHasher<object>().HashPassword(new object(),password)}}};
await File.WriteAllTextAsync(configPath,JsonSerializer.Serialize(config,new JsonSerializerOptions{WriteIndented=true}));
Directory.CreateDirectory(Path.GetDirectoryName(access)!);
await File.WriteAllTextAsync(access,$"# Acceso al panel local\n\nSolo desarrollo; todos los pedidos iniciales son de demostración.\n\n- URL: http://localhost:5180/admin/\n- Email: {email}\n- Contraseña: `{password}`\n- Base nueva: `{database}`\n\nIniciar desde gastronomia-saas con:\n\n```powershell\n$env:ASPNETCORE_ENVIRONMENT='Development'\ndotnet run --project src/Gastronomia.Api --no-launch-profile --urls http://localhost:5180\n```\n\nLa contraseña no se guarda en el repositorio. El archivo appsettings.Development.local.json contiene únicamente su hash y la configuración local. No publicar ese archivo ni este documento.\n");
Console.WriteLine("Panel de demostración inicializado en una base nueva. Datos de acceso guardados en el archivo solicitado; no se imprimen credenciales.");
sealed class DevFactory(DbContextOptions<GastronomiaDbContext> options,TenantContext scope) : IDbContextFactory<GastronomiaDbContext>{public GastronomiaDbContext CreateDbContext()=>new(options,scope);public Task<GastronomiaDbContext> CreateDbContextAsync(CancellationToken ct=default)=>Task.FromResult(CreateDbContext());}
