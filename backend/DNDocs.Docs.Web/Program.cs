using DNDocs.Docs.Api.Client;
using DNDocs.Docs.Web.Application;
using DNDocs.Docs.Web.Infrastructure;
using DNDocs.Docs.Web.Model;
using DNDocs.Docs.Web.Service;
using DNDocs.Docs.Web.Shared;
using DNDocs.Docs.Web.Web;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.Routing.Patterns;
using System.Diagnostics;
using Vinca.Http.Cache;
using Vinca.Http.Logs;
using Vinca.RateLimit;
using Vinca.TickLogger;
using Vinca.Utils;

namespace DNDocs.Docs.Web
{
    class Program
    {
        static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

#if DEBUG
            SetupIfIntegrationTests(builder);
#endif


            // Add services to the container.
            var settings = new DSettings();
            builder.WebHost.UseKestrel(o =>
            {
                o.Limits.MaxRequestBodySize = 1024 * 1024 * 300;
                o.AddServerHeader = false;
            });

            // config
            builder.Services.Configure<DFileSystemOptions>(builder.Configuration.GetSection($"{nameof(DSettings)}:{nameof(DFileSystemOptions)}"));
            builder.Services.Configure<DSettings>(builder.Configuration.GetSection($"{nameof(DSettings)}"));
            builder.Configuration.GetSection($"{nameof(DSettings)}").Bind(settings);

            ValidateAppsettings.Validate(typeof(DSettings), builder.Configuration);

            // external services
            builder.Services.AddResourceMonitoring();
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddLogging();
            builder.Services.AddVHttpLogs(o =>
            {
                o.ShouldSaveLog = IgnoreHttpLogsFor;
                o.OnSaveLogs = (sp, logs) => sp.GetRequiredService<ILogsService>().OnVHttpLogSaveLogs(logs);
            });

            builder.Services.AddVCacheControlService(o =>
            {
                // todo
                // o.GetCacheResourceInfo = (ctx) => { ctx.RequestServices.GetRequiredKeyedService<> };
            });

            //dndocs.docs services
            
            builder.Services.AddTransient<IManagementControllerContext, ManagementControllerContext>();
            builder.Services.AddHostedService<DHostedService>();
            builder.Services.AddDNDDInfrastructure();
            builder.Services.AddVincaTickLogger(x =>
            {
                x.MaxLogsTreshold = 10000;
                x.TimerTickTimeSpan = settings.SaveAppLogsTimeSpan;
                x.OnSaveLogs = (d) => d.ServiceProvider.GetRequiredService<ILogsService>().TickLoggerSaveLogs(d);
            });

            builder.Services.AddSingleton<ILogsService, LogsService>();
            builder.Services.AddScoped<IManagementService, ManagementService>();
            builder.Services.AddSingleton<IQRepository, QRepository>();
            builder.Services.AddScoped<ITxRepository, TxRepository>();

#if DEBUG
            // add validation service acn be resolved
#endif

            var app = builder.Build();
            app.StartVincaTickLogger();
            app.DInfrastructureAppBuilded();

            // Configure the HTTP request pipeline.

            app.UseVHttpLogs();
            app.UseMiddleware<DMiddleware>();
            app.UseHttpsRedirection();
            // app.UseVRateLimit()
            app.UseVCacheControl();

            var allEndpoints = new List<ApiEndpoint>();
            allEndpoints.AddRange(ManagementController.Endpoints);
            allEndpoints.AddRange(PublicContentController.Endpoints);

            foreach (var e in allEndpoints)
            {
                if (e.HttpMethod == HttpMethod.Get) app.MapGet(e.Route, e.Delegate);
                else if (e.HttpMethod == HttpMethod.Post) app.MapPost(e.Route, e.Delegate).DisableAntiforgery();
                else throw new Exception($"Startup exception, unknown HttpMethod to bind: '{e.HttpMethod?.ToString()}' on route '{e.Route}'");
            }


            //byte[] filebytes = File.ReadAllBytes(@"C:\Users\user\Desktop\ef_site.zip");

            // #if DEGUB
            //for (int i = 0; i < 1; i++)
            //{
            //    Task.Factory.StartNew(async () =>
            //    {
            //        var id = new Random().Next(1000123);
            //        // var stre = new FileStream(@"C:\Users\user\Desktop\_site.zip", FileMode.Open);
            //        var stre = new MemoryStream();
            //        stre.Write(filebytes);
            //        stre.Position = 0;
            //        stre.Position = 0;
            //        new DndocsDocsApiClient(new DndocsApiClientOptions { ApiKey = "@T4hjr4dsh$%H$J%45j6t7kY^zsdg34", ServerUrl = "https://localhost:7088" }, new asdf())
            //        .CreateOrReplaceProject(id, "pname", null, null, id.ToString(), id.ToString(), 3, "dndocs-ver1",
            //        stre).Wait();
            //    });
            //}
            
            // #endif
           
            
           app.Run();
        }


        static bool IgnoreHttpLogsFor(HttpContext context)
        {
            var path = context.Request.Path.Value;
            if ((context.Response.StatusCode == 200 || context.Response.StatusCode == 304) && path != null)
            {
                if (
                path.StartsWith("/n/") ||
                path.StartsWith("/v/") ||
                path.StartsWith("/s/"))
                {
                    return true;
                }
            }

            return false;
        }

        class asdf : IHttpClientFactory
        {
            public HttpClient CreateClient(string name)
            {
                return new HttpClient();
            }
        }


#if DEBUG
        private static void SetupIfIntegrationTests(WebApplicationBuilder builder)
        {
            // trick for integration tests to attach VS debugger
            var a = builder.Configuration.GetValue<string>("ASPNETCORE_ISINTEGRATIONTESTS");

            if (a?.ToLower() == "true")
            {
                //var infrastructure = builder.Configuration.GetSection("DSettings:DFileSystemOptions:InfrastructureFolderOSPath").Value;

                //if (infrastructure?.EndsWith("temp\\it-ddocs"))
                //{
                    
                //}

                // throw new Exception("ittest");
                // Debugger.Launch();
            }
        }
#endif

    }
}
