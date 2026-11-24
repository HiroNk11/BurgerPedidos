using System.Data;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

static string Required(string key) => Environment.GetEnvironmentVariable(key) is {Length: >0} value ? value : throw new InvalidOperationException($"Configurar {key}.");
var name = Required("DatabaseBootstrap__Name");
if (!Regex.IsMatch(name,"^[A-Za-z][A-Za-z0-9_]{0,63}$")) throw new InvalidOperationException("Nombre de base inválido.");
var appPassword = Required("DatabaseBootstrap__AppPassword");
var tenantName = Required("DatabaseBootstrap__TenantName"); var whatsapp = Required("DatabaseBootstrap__WhatsApp");
if (!Guid.TryParse(Required("DatabaseBootstrap__TenantId"),out var tenantId) || tenantId == Guid.Empty || tenantName.Length > 120 || !Regex.IsMatch(whatsapp,"^[0-9]{10,15}$")) throw new InvalidOperationException("Datos del local inválidos.");
var builder = new SqlConnectionStringBuilder { DataSource="db,1433", InitialCatalog="master", UserID="sa", Password=Required("DatabaseBootstrap__SaPassword"), Encrypt=true, TrustServerCertificate=true, ConnectTimeout=15 };
await using var master = new SqlConnection(builder.ConnectionString); await master.OpenAsync();
await using var gate = master.CreateCommand();
gate.CommandText="DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=@resource,@LockMode='Exclusive',@LockOwner='Session',@LockTimeout=60000; SELECT @r;";
gate.Parameters.AddWithValue("@resource","gastronomia-schema:"+name);
if (Convert.ToInt32(await gate.ExecuteScalarAsync()) < 0) throw new InvalidOperationException("Otra migración está en curso.");
// El finally libera el lock antes de devolver la conexión al pool.
try
{
    await using(var create=master.CreateCommand())
    {
        create.CommandText="IF DB_ID(@name) IS NULL BEGIN DECLARE @sql nvarchar(max)=N'CREATE DATABASE '+QUOTENAME(@name); EXEC(@sql); END";
        create.Parameters.AddWithValue("@name",name); await create.ExecuteNonQueryAsync();
    }
    builder.InitialCatalog=name;
    await using var db=new SqlConnection(builder.ConnectionString);await db.OpenAsync();
    await using(var ledger=db.CreateCommand()) {ledger.CommandText="IF OBJECT_ID(N'dbo.SchemaVersions',N'U') IS NULL CREATE TABLE dbo.SchemaVersions(Name nvarchar(120) NOT NULL PRIMARY KEY, AppliedUtc datetimeoffset NOT NULL DEFAULT SYSDATETIMEOFFSET());";await ledger.ExecuteNonQueryAsync();}
    var scripts=Path.Combine(AppContext.BaseDirectory,"sql"); if(!Directory.Exists(scripts)) scripts=Path.Combine(Environment.CurrentDirectory,"sql");
    foreach(var file in new[]{"001_schema.sql","003_checkout_notifications.sql","004_customer_accounts.sql","005_engagement_payments.sql","006_mercadopago_checkout.sql","002_history_guards.sql"})
    {
        await using var seen=db.CreateCommand();seen.CommandText="SELECT COUNT(*) FROM dbo.SchemaVersions WHERE Name=@name";seen.Parameters.AddWithValue("@name",file);
        if(Convert.ToInt32(await seen.ExecuteScalarAsync())>0)continue;
        // Bases existentes sin ledger: nunca ejecutar el CREATE completo sobre tablas operativas.
        await using var tables=db.CreateCommand();tables.CommandText="SELECT OBJECT_ID(N'dbo.Tenants',N'U')";
        bool skip=file=="001_schema.sql" && await tables.ExecuteScalarAsync() is not DBNull and not null;
        if(!skip)
        {
            var sql=await File.ReadAllTextAsync(Path.Combine(scripts,file));
            foreach(var batch in Regex.Split(sql,@"^\s*GO\s*$",RegexOptions.Multiline|RegexOptions.IgnoreCase))
                if(!string.IsNullOrWhiteSpace(batch)){await using var run=db.CreateCommand();run.CommandText=batch;run.CommandTimeout=120;await run.ExecuteNonQueryAsync();}
        }
        await using var record=db.CreateCommand();record.CommandText="INSERT INTO dbo.SchemaVersions(Name) VALUES(@name)";record.Parameters.AddWithValue("@name",file);await record.ExecuteNonQueryAsync();
    }
    await using(var seed=db.CreateCommand())
    {
        seed.CommandText="IF NOT EXISTS(SELECT 1 FROM dbo.Tenants WHERE Id=@id) INSERT INTO dbo.Tenants(Id,Nombre,TimeZoneId,WhatsApp,Moneda,TarifaEnvio,Activo) VALUES(@id,@name,N'America/Argentina/Buenos_Aires',@phone,N'ARS',0,1)";
        seed.Parameters.AddWithValue("@id",tenantId);seed.Parameters.AddWithValue("@name",tenantName);seed.Parameters.AddWithValue("@phone",whatsapp);await seed.ExecuteNonQueryAsync();
    }
    await using(var login=master.CreateCommand())
    {
        // Identificador fijo y password escapado por QUOTENAME; nunca concatenar el secreto como SQL crudo.
        login.CommandText="DECLARE @sql nvarchar(max); IF SUSER_ID(N'gastronomia_api') IS NULL SET @sql=N'CREATE LOGIN [gastronomia_api] WITH PASSWORD='+QUOTENAME(@password,CHAR(39))+N', CHECK_POLICY=ON'; ELSE SET @sql=N'ALTER LOGIN [gastronomia_api] WITH PASSWORD='+QUOTENAME(@password,CHAR(39)); EXEC(@sql);";
        if(appPassword.Length>128)throw new InvalidOperationException("Password SQL demasiado largo.");
        login.Parameters.Add("@password",SqlDbType.NVarChar,128).Value=appPassword;await login.ExecuteNonQueryAsync();
    }
    await using(var user=db.CreateCommand())
    {
        user.CommandText="IF USER_ID(N'gastronomia_api') IS NULL CREATE USER [gastronomia_api] FOR LOGIN [gastronomia_api]; IF IS_ROLEMEMBER(N'db_datareader',N'gastronomia_api')<>1 ALTER ROLE db_datareader ADD MEMBER [gastronomia_api]; IF IS_ROLEMEMBER(N'db_datawriter',N'gastronomia_api')<>1 ALTER ROLE db_datawriter ADD MEMBER [gastronomia_api];";
        await user.ExecuteNonQueryAsync();
    }
    Console.WriteLine("Esquema actualizado, tenant inicializado y usuario de aplicación preparado. No se cargaron productos ni pedidos demo.");
}
finally
{
    await using var release=master.CreateCommand();release.CommandText="EXEC sys.sp_releaseapplock @Resource=@resource,@LockOwner='Session'";release.Parameters.AddWithValue("@resource","gastronomia-schema:"+name);await release.ExecuteNonQueryAsync();
}
