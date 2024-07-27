using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vinca.Http.Logs
{
    internal class VHttpLogsMiddleware
    {
        private RequestDelegate next;
        private IVHttpLogService vhlService;

        public VHttpLogsMiddleware(
            IVHttpLogService vhlService,
            RequestDelegate next,
            ILogger<VHttpLogsMiddleware> logger)
        {
            this.next = next;
            this.vhlService = vhlService;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // todo object pool for timers & http
            var stopwatch = Stopwatch.StartNew();
            
            await next(context);

            stopwatch.Stop();

            if (vhlService.ShouldSaveLog(context))
            {
                var now = DateTime.UtcNow;
                var log = new VHttpLog()
                {
                    Date = now.ToString("yyyy/MM/dd"),
                    Time = now.ToString("HH:mm:ss"),
                    ClientIP = context.Connection.RemoteIpAddress.ToString(),
                    ClientPort = context.Connection.RemotePort,
                    Method = context.Request.Method,
                    UriPath = context.Request.Path,
                    UriQuery = context.Request.QueryString.ToString(),
                    ResponseStatus = context.Response.StatusCode,
                    BytesSend = context.Response.ContentLength,
                    TimeTakenMs = stopwatch.ElapsedMilliseconds,
                    Host = context.Request.Headers.Host.ToString(),
                    UserAgent = context.Request.Headers.UserAgent.ToString(),
                    Referer = context.Request.Headers.Referer.ToString()
                };

                vhlService.SaveLog(log);
            }
        }
    }
}
