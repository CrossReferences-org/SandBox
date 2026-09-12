using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using SandBox.Components;
using SandBox.Services.ConnectionExplorer;

namespace SandBox
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddRazorComponents()
                .AddInteractiveServerComponents();

            builder.Services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                // Cleared to ensure it trusts the reverse proxy in a typical Linux/Cloudflare deployment
                options.KnownIPNetworks.Clear();
                options.KnownProxies.Clear();
            });

            var jsonPath = builder.Configuration["JsonPath"]
                ?? throw new InvalidOperationException("JsonPath is not configured in appsettings.json.");

            if (!Path.IsPathRooted(jsonPath))
                jsonPath = Path.Combine(builder.Environment.ContentRootPath, jsonPath);

            var dataCache = new DataCache(jsonPath);

            builder.Services.AddSingleton(new ConnectionExplorerService(dataCache));

            var keysPath = Path.Combine(builder.Environment.ContentRootPath, "keys");

            builder.Services.AddDataProtection()
                .PersistKeysToFileSystem(new DirectoryInfo(keysPath))
                .SetApplicationName("SandBox");

            var app = builder.Build();

            // 4. Place Forwarded Headers early in the HTTP pipeline
            app.UseForwardedHeaders();

            var pathBase = builder.Configuration["PathBase"];
            if (!string.IsNullOrEmpty(pathBase))
                app.UsePathBase(pathBase);

            // 5. Run your custom Serilog-backed middleware

            _ = app.Services.GetRequiredService<ConnectionExplorerService>();



            app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
            //app.UseHttpsRedirection();

            app.UseAntiforgery();

            app.MapStaticAssets();
            app.MapRazorComponents<App>()
                .AddInteractiveServerRenderMode();

            app.Run();
        }
    }
}