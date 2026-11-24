namespace Gastronomia.Api;
public sealed class LocalStaffOptions {
 public bool Enabled {get;set;}
 public Guid TenantId {get;set;}
 public Guid UserId {get;set;}
 public string Email {get;set;} = "";
 public string PasswordHash {get;set;} = "";
}
