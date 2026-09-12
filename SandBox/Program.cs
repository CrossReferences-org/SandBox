using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using SandBox.Components;
using SandBox.Helpers;
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
                // Trusts X-Forwarded-* from any upstream. That's safe only when the app is
                // unreachable except through the proxy, which is the case in this deployment.
                options.KnownIPNetworks.Clear();
                options.KnownProxies.Clear();
            });

            var jsonPath = builder.Configuration["JsonPath"]
                ?? throw new InvalidOperationException("JsonPath is not configured in appsettings.json.");

            if (!Path.IsPathRooted(jsonPath))
                jsonPath = Path.Combine(builder.Environment.ContentRootPath, jsonPath);

            var dataCache = new DataCache(jsonPath);

            builder.Services.AddSingleton(new ConnectionExplorerService(dataCache));

            // Keys are persisted on the server so circuits and antiforgery tokens survive
            // a restart. Locally there's nothing worth persisting, so fall back to the
            // framework default (~/.aspnet/DataProtection-Keys) rather than failing.
            var keysPath = builder.Configuration["DataProtectionKeysPath"];

            var dataProtection = builder.Services.AddDataProtection()
                                        .SetApplicationName("SandBox");

            if (MiscHelpers.TryPrepareKeyDirectory(keysPath, out DirectoryInfo? keysDir))
                dataProtection.PersistKeysToFileSystem(keysDir!);

            var app = builder.Build();

            app.UseForwardedHeaders();

            var pathBase = builder.Configuration["PathBase"];
            if (!string.IsNullOrEmpty(pathBase))
                app.UsePathBase(pathBase);

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