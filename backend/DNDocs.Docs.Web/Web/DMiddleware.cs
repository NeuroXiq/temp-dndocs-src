using DNDocs.Docs.Web.Service;
using DNDocs.Docs.Web.Shared;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Net;
using Vinca.Utils;

namespace DNDocs.Docs.Web.Web
{
    public class DMiddleware
    {
        private RequestDelegate next;
        private ILogger<DMiddleware> logger;
        private ILogsService logsService;
        private string apiKey;

        public DMiddleware(
            RequestDelegate next,
            ILogger<DMiddleware> logger,
            ILogsService logsService,
            IOptions<DSettings> settings)
        {
            this.next = next;
            this.logger = logger;
            this.logsService = logsService;
            this.apiKey = settings.Value.DNDocsDocsApiKey;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await next(context);
            }
            catch (DValidationException e)
            {
                logger.LogError("Validation error: {0} ", e.Message);
                context.Response.StatusCode = StatusCodes.Status400BadRequest;

                context.Response.ContentType = "application/json; charset=UTF-8";
                await context.Response.WriteAsJsonAsync(new { ValidationError = e.Message });
            }
            catch (DUnauthorizedException e)
            {
                context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;    
            }
            catch (Exception e)
            {
                logger.LogCritical(e, "ISE");
            }
        }
    }
}
