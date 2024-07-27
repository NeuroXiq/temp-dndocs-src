using DNDocs.Docs.Api.Management;
using DNDocs.Docs.Web.Infrastructure;
using DNDocs.Docs.Web.Service;
using DNDocs.Docs.Web.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Net;
using System.Net.Mime;
using Vinca.Utils;

namespace DNDocs.Docs.Web.Web
{
    public interface IManagementControllerContext
    {
        ILogger<ManagementController> Logger { get; }
        HttpContext HttpContext { get; }
        IOptions<DSettings> Settings { get; }
        IManagementService ManagementService { get; }
    }

    public class ManagementControllerContext : IManagementControllerContext
    {
        public ILogger<ManagementController> Logger { get; set; }

        public HttpContext HttpContext { get; set; }

        public IOptions<DSettings> Settings { get; set; }

        public IManagementService ManagementService { get; set; }

        public ManagementControllerContext(ILogger<ManagementController> logger,
            IHttpContextAccessor httpContext,
            IOptions<DSettings> settings,
            IManagementService managementService)
        {
            Logger = logger;
            HttpContext = httpContext.HttpContext;
            Settings = settings;
            ManagementService = managementService;
        }
    }

    public class ManagementController
    {
        public const string Controller = "/api/management";

        public static readonly ApiEndpoint[] Endpoints = new ApiEndpoint[]
        {
            GetEndpoint(HttpMethod.Get, "/ping/{reply?}", Ping),
            GetEndpoint(HttpMethod.Post, "/createproject", CreateProject),
            GetEndpoint(HttpMethod.Get, "/deleteproject", DeleteProject),
        };

        private static async Task<IResult> DeleteProject([FromBody]DeleteProjectModel model, [FromServices] IManagementControllerContext mmc)
        {
            Authorized(mmc);
            await mmc.ManagementService.DeleteProject(model.ProjectId);

            return Results.Ok();
        }

        static ApiEndpoint GetEndpoint(HttpMethod method, string path, Delegate delegateMethod)
        {
            return new ApiEndpoint(method, $"{Controller}{path}", delegateMethod);
        }
        
        static async Task<IResult> CreateProject([FromForm] CreateProjectModel m, [FromServices] IManagementControllerContext mmc)
        {
            Authorized(mmc);

            using var tempFile = DTempFile.Create();
            using var stream = File.Create(tempFile.FilePath);
            await m.SiteZip.CopyToAsync(stream);

            await mmc.ManagementService.CreateProject(
                m.ProjectId,
                m.Metadata,
                m.ProjectName,
                m.UrlPrefix,
                m.PVVersionTag,
                m.NPackageName,
                m.NPackageVersion,
                (Model.ProjectType)m.ProjectType,
                stream);

            return Results.Ok();
        }

        static IResult Ping([FromServices] IManagementControllerContext mcContext, string reply)
        {
            Authorized(mcContext);

            return Results.Content(reply ?? "", "text/plain; charset=UTF-8");
        }

        private static void Authorized(IManagementControllerContext mcContext)
        {
            var logger = mcContext.Logger;
            var context = mcContext.HttpContext;
            var apiKey = mcContext.Settings.Value.DNDocsDocsApiKey;

            Exception exc = null;

            try
            {
                if (context.Request.Headers.TryGetValue("x-api-key", out var apiKeyHeader))
                {
                    if (apiKeyHeader.Count == 1 && apiKeyHeader[0] == apiKey)
                    {
                        return;
                    }
                }

                logger.LogWarning("unauthorized, path: {0}, remote ip: {1}, remote port: {2}, headers:\r\n{3} ",
                    context.Request.Path,
                    context.Connection.RemoteIpAddress.ToString(),
                    context.Connection.RemotePort,
                    context.Request.Headers.Select(t => $"{t.Key}: {t.Value.StringJoin(", ")}\r\n"));

                context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            }
            catch (Exception e)
            {
                exc = e;
            }

            throw new DUnauthorizedException();
        }
    }
}
