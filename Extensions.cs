using Constellations;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Security.Cryptography.X509Certificates;

namespace DotJS
{
    public static class Extensions
    {
        public static IServiceCollection AddJS(this IServiceCollection services, X509Certificate? X509Cert = null)
        {
            var name = Guid.NewGuid().ToString();
            var key = Guid.NewGuid().ToString();
            
            var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
            var constellation = new Constellation(name)
                                    .ListenOn(name, (environment != "Production") ? "127.0.0.1" : IPAddress.Any.ToString(), 65231)
                                    .SetKey(key)
                                    .AllowOrigins("*")
                                    .NoBroadcasting()
                                    .AsServer();
            if(X509Cert is not null)
            {
                constellation.SetX509Cert(X509Cert);
            }
                                    constellation.Run();
            services.AddScoped<JS>(options =>
            {
                
                return new JS(Guid.NewGuid().ToString(), constellation, Guid.NewGuid().ToString());
            });
            return services;
        }
    }
}
