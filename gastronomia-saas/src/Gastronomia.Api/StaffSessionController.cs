using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
namespace Gastronomia.Api;
[ApiController,Route("api/staff")]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class StaffSessionController(IOptions<LocalStaffOptions> options,IHostEnvironment env,IAntiforgery antiforgery) : ControllerBase {
 public const string Scheme="LocalStaff";
 private bool Enabled => env.IsDevelopment() && options.Value.Enabled;
 [HttpGet("session")]
 public IActionResult Session() {
  var tokens=antiforgery.GetAndStoreTokens(HttpContext);
  return Ok(new {authenticated=User.Identity?.IsAuthenticated==true,localLogin=Enabled,email=User.FindFirstValue("email"),tenantId=User.FindFirstValue("tenant_id"),csrfToken=tokens.RequestToken});
 }
 [HttpPost("login"),EnableRateLimiting("staff-login"),ValidateAntiForgeryToken]
 public async Task<IActionResult> Login(LoginRequest request) {
  if(!Enabled) return NotFound();
  var config=options.Value;
  // Verificar el hash incluso si el email es incorrecto; no revelar cuentas.
  var result=new PasswordHasher<object>().VerifyHashedPassword(new object(),config.PasswordHash,request.Password);
  if(!string.Equals(config.Email,request.Email.Trim(),StringComparison.OrdinalIgnoreCase) || result==PasswordVerificationResult.Failed) return Unauthorized(new {title="Email o contraseña incorrectos."});
  var identity=new ClaimsIdentity(new[]{new Claim("sub",config.UserId.ToString()),new Claim("tenant_id",config.TenantId.ToString()),new Claim("role","staff"),new Claim(ClaimTypes.Role,"staff"),new Claim("email",config.Email),new Claim("email_verified","true")},Scheme);
  await HttpContext.SignInAsync(Scheme,new ClaimsPrincipal(identity),new AuthenticationProperties{IsPersistent=false,AllowRefresh=false});
  return NoContent();
 }
 [HttpPost("logout"),ValidateAntiForgeryToken]
 public async Task<IActionResult> Logout() {if(!Enabled) return NotFound();await HttpContext.SignOutAsync(Scheme);return NoContent();}
}
public sealed record LoginRequest([Required,EmailAddress,MaxLength(254)]string Email,[Required,MinLength(12),MaxLength(128)]string Password);
