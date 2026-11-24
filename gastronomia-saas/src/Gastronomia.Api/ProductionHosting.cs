using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
namespace Gastronomia.Api;

internal static class ProductionHosting
{
    public static void Configure(WebApplicationBuilder builder)
    {
        var proxy = builder.Configuration["ReverseProxy:KnownProxy"];
        if (!string.IsNullOrWhiteSpace(proxy))
        {
            if (!IPAddress.TryParse(proxy, out var address)) throw new InvalidOperationException("ReverseProxy__KnownProxy requiere una IP concreta.");
            builder.Services.Configure<ForwardedHeadersOptions>(o =>
            {
                o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                o.ForwardLimit = 1; o.RequireHeaderSymmetry = true;
                o.KnownProxies.Clear(); o.KnownProxies.Add(address);
                o.KnownIPNetworks.Clear();
            });
        }
        if (!builder.Environment.IsDevelopment())
        {
            builder.Services.AddHttpsRedirection(o => o.HttpsPort = 443);
            if (string.IsNullOrWhiteSpace(proxy)) throw new InvalidOperationException("Configurar ReverseProxy__KnownProxy y un proxy HTTPS delante del puerto 8080.");
            var hosts = builder.Configuration["AllowedHosts"];
            if (string.IsNullOrWhiteSpace(hosts) || hosts.Contains('*')) throw new InvalidOperationException("Configurar AllowedHosts con el dominio público de la API.");
            if (builder.Configuration["Email:Provider"] == "Resend" && (string.IsNullOrWhiteSpace(builder.Configuration["Email:ResendApiKey"]) || string.IsNullOrWhiteSpace(builder.Configuration["Email:From"])))
                throw new InvalidOperationException("Configurar Email__ResendApiKey y Email__From verificado.");
        }
    }
}
