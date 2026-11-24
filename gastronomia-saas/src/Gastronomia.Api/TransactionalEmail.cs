using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Gastronomia.Api;
public sealed record OutgoingEmail(Guid TenantId,Guid EventId,Guid OrderId,string ShortCode,string To,string Subject,string Text,DateTimeOffset CreatedUtc);
public interface ITransactionalEmailSender {Task<string> SendAsync(OutgoingEmail email,CancellationToken ct);}
public sealed class LocalMailboxEmailSender(IWebHostEnvironment env):ITransactionalEmailSender {
 public string DirectoryFor(Guid tenant)=>Path.Combine(env.ContentRootPath,"App_Data","mail",tenant.ToString("N"));
 public async Task<string> SendAsync(OutgoingEmail email,CancellationToken ct){
  var directory=DirectoryFor(email.TenantId);Directory.CreateDirectory(directory);var path=Path.Combine(directory,email.EventId.ToString("N")+".json");
  if(File.Exists(path))return "local-"+email.EventId;
  var temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
  try{await File.WriteAllTextAsync(temporary,JsonSerializer.Serialize(email),ct);try{File.Move(temporary,path,false);}catch(IOException)when(File.Exists(path)){} }finally{if(File.Exists(temporary))File.Delete(temporary);}
  return "local-"+email.EventId;
 }
}
public sealed class ResendEmailSender(HttpClient http,IConfiguration config):ITransactionalEmailSender{
 public async Task<string> SendAsync(OutgoingEmail email,CancellationToken ct){
  string key=config["Email:ResendApiKey"]??Environment.GetEnvironmentVariable("RESEND_API_KEY")??throw new InvalidOperationException("Configurar la clave de Resend en el servidor.");
  string from=config["Email:From"]??throw new InvalidOperationException("Configurar un remitente verificado.");
  using var request=new HttpRequestMessage(HttpMethod.Post,"https://api.resend.com/emails");request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",key);request.Headers.Add("Idempotency-Key","order-event-"+email.EventId);
  request.Content=JsonContent.Create(new{from,to=new[]{email.To},subject=email.Subject,text=email.Text});
  using var response=await http.SendAsync(request,ct);if(!response.IsSuccessStatusCode)throw new HttpRequestException("Resend rechazó el envío: HTTP "+(int)response.StatusCode);
  var document=await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken:ct);return document.GetProperty("id").GetString()??throw new InvalidOperationException("Resend no devolvió identificador.");
 }
}
[ApiController,Authorize(Policy="Staff"),Route("api/tenants/{tenantId:guid}/staff/mailbox")]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class LocalMailboxController(IWebHostEnvironment env,IConfiguration config):ControllerBase{
 [HttpGet]
 public async Task<IActionResult> List(Guid tenantId,CancellationToken ct){if(!env.IsDevelopment()||!string.Equals(config["Email:Provider"]??"Local","Local",StringComparison.OrdinalIgnoreCase))return NotFound();var directory=Path.Combine(env.ContentRootPath,"App_Data","mail",tenantId.ToString("N"));var result=new List<OutgoingEmail>();if(Directory.Exists(directory))foreach(var file in Directory.EnumerateFiles(directory,"*.json").OrderByDescending(System.IO.File.GetLastWriteTimeUtc).Take(100)){await using var stream=System.IO.File.OpenRead(file);var email=await JsonSerializer.DeserializeAsync<OutgoingEmail>(stream,cancellationToken:ct);if(email?.TenantId==tenantId)result.Add(email);}return Ok(result.OrderByDescending(e=>e.CreatedUtc));}
}
