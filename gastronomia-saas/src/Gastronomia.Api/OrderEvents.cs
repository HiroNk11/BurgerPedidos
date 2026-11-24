using System.Collections.Concurrent;
using System.Threading.Channels;
using System.Security.Claims;
using Gastronomia.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Gastronomia.Api;
public sealed class OrderEvents {
 private readonly ConcurrentDictionary<Guid,ConcurrentDictionary<Guid,Channel<bool>>> subscriptions=new();
 public (Guid Id,Channel<bool> Channel) Subscribe(Guid tenant) {var id=Guid.NewGuid();var channel=Channel.CreateBounded<bool>(new BoundedChannelOptions(1){FullMode=BoundedChannelFullMode.DropOldest});subscriptions.GetOrAdd(tenant,_=>new())[id]=channel;return(id,channel);}
 public void Remove(Guid tenant,Guid id) {if(subscriptions.TryGetValue(tenant,out var channels)&&channels.TryRemove(id,out var c))c.Writer.TryComplete();}
 public void Publish(Guid tenant) {if(subscriptions.TryGetValue(tenant,out var channels))foreach(var c in channels.Values)c.Writer.TryWrite(true);}
}
[ApiController,Authorize(Policy="Staff"),Route("api/tenants/{tenantId:guid}/staff/events")]
public sealed class OrderEventsController(OrderEvents events,TenantContext tenant):ControllerBase {
 [HttpGet]
 public async Task Stream(CancellationToken ct) {
  Response.ContentType="text/event-stream";Response.Headers.CacheControl="no-store";Response.Headers["X-Accel-Buffering"]="no";
  var subscription=events.Subscribe(tenant.TenantId);
  try {
   await Response.WriteAsync("data: connected\n\n",ct);await Response.Body.FlushAsync(ct);
   while(!ct.IsCancellationRequested) {
    using var heartbeat=CancellationTokenSource.CreateLinkedTokenSource(ct);heartbeat.CancelAfter(TimeSpan.FromSeconds(20));
    bool available;
    try {available=await subscription.Channel.Reader.WaitToReadAsync(heartbeat.Token);}catch(OperationCanceledException) when(!ct.IsCancellationRequested){await Response.WriteAsync(": ping\n\n",ct);await Response.Body.FlushAsync(ct);continue;}
    if(!available)break;while(subscription.Channel.Reader.TryRead(out _)){}
    await Response.WriteAsync("data: ordersChanged\n\n",ct);await Response.Body.FlushAsync(ct);
   }
  }catch(OperationCanceledException) when(ct.IsCancellationRequested){}finally{events.Remove(tenant.TenantId,subscription.Id);}
 }
}