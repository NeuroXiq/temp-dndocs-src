using Microsoft.AspNetCore.Http.Extensions;
using DNDocs.Application.Application;
using DNDocs.Domain.Entity.App;
using DNDocs.Shared.Log;
using System.Text;

namespace DNDocs.Web.Application
{
    public class LogHttpRequestMiddleware
    {
        private readonly RequestDelegate next;

        static string[] HeadersToLog = new string[]
        {

        };

        public LogHttpRequestMiddleware(RequestDelegate next)
        {
            this.next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // todo remove this and user directly logger.log(...)  ??
            // is http_log table need to be separated? (this logs with context 'loghttprequestmiddleware' anyway)
            // but maybe useful becaus of dirrerent columns in http_log table only related with http request

            try
            {
                await next(context);
            }
            catch (Exception e)
            {
                throw;
            }
            finally
            {
                var log = await CreateLog(context);
                ApiBackgroundWorker.HttpLog(log);
            }
        }

        private async Task<HttpLog> CreateLog(HttpContext context)
        {
            var req = context.Request;
            var bodyPart = new byte[1024];
            int bodyLen = 0;

            try
            {
                req.EnableBuffering();
                bodyLen = await req.Body.ReadAsync(bodyPart, 0, 1024); // sometimes request aborted exception
                req.Body.Position = 0;
            }
            catch { }
            

            var method = context.Request.Method;
            var encodedUrl = context.Request.GetEncodedUrl();
            var path = req.Path.ToString(); // log
            var headersKeyValues = req.Headers.Select(h => $"{h.Key}={string.Join(", ", h.Value.Select(v => v).ToArray())}");
            var headers = string.Join("\r\n", headersKeyValues);

            string remoteIp = null;

            if (context.Request.Headers.ContainsKey("X-Forwarded-For"))
            {
                remoteIp = context.Request.Headers["X-Forwarded-For"].ToString();
            }
            else
            {
                remoteIp = context.Connection.RemoteIpAddress?.ToString() ?? "<empty-ip>";
            }

            var datetime = DateTime.Now.ToUniversalTime().ToString("yyyy MM dd HH:mm:ss.fff Z ");

            var httpLog = new HttpLog();

            httpLog.DateTime = DateTime.UtcNow;
            httpLog.Headers = headers;
            httpLog.IP = remoteIp;
            httpLog.Method = method;
            httpLog.Path = path;
            httpLog.Payload = Encoding.ASCII.GetString(bodyPart, 0, bodyLen);

            return httpLog;
        }
    }
}