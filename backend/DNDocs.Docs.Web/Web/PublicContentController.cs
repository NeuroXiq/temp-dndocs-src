using DNDocs.Docs.Web.Model;
using DNDocs.Docs.Web.Service;
using DNDocs.Docs.Web.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Diagnostics.ResourceMonitoring;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using Vinca.Http;
using Vinca.Http.Cache;

namespace DNDocs.Docs.Web.Web
{
    public class PublicContentController
    {
        public static readonly ApiEndpoint[] Endpoints = new ApiEndpoint[]
        {
            GetEndpoint(HttpMethod.Get, "/n/{nugetPackageName}/{nugetPackageVersion}/{*slug}", GetNugetProjectSiteHtml),
            GetEndpoint(HttpMethod.Get, "/v/{urlPrefix}/{versionTag}/{*slug}", GetVersionProjectSiteHtml),
            GetEndpoint(HttpMethod.Get, "/s/{urlPrefix}/{*slug}", GetSingletonProjectSiteHtml),
            GetEndpoint(HttpMethod.Get, "/public/{*slug}", GetPublicHtmlFile),
            GetEndpoint(HttpMethod.Get, "/public/ping", Ping),
            GetEndpoint(HttpMethod.Get, "/sitemap.xml", GetSiteMap),
            GetEndpoint(HttpMethod.Get, "/robots.txt", GetRobotsTxt),
            GetEndpoint(HttpMethod.Get, "/system/projects/{pageNo?}", SystemAllProjects),
            GetEndpoint(HttpMethod.Get, "/system/site-items/{pageNo?}", SystemSiteItems),
            GetEndpoint(HttpMethod.Get, "/system/stats", SystemStats),
            GetEndpoint(HttpMethod.Get, "/system/resource-monitor", SystemResourceMonitoring),
        };

        static ApiEndpoint GetEndpoint(HttpMethod method, string path, Delegate delegateMethod)
        {
            return new ApiEndpoint(method, path, delegateMethod);
        }

        static IResult Ping()
        {
            return Results.Ok();
        }

        static async Task<IResult> GetRobotsTxt(
            [FromServices] ILogger<PublicContentController> logger,
            [FromServices] IWebHostEnvironment env) => await GetPublicHtmlFile(logger, env, "/public/robots.txt");

        static async Task<IResult> GetPublicHtmlFile(
            [FromServices] ILogger<PublicContentController> logger,
            [FromServices] IWebHostEnvironment env,
            string slug)
        {
            //todo add everyting from PublicHtml to site.sqlite db on startup
            var publicHtmlDir = env.ContentRootPath;
            var filepath = Path.Combine(publicHtmlDir, "PublicHtml", slug);
            if (!File.Exists(filepath)) return Results.NotFound();

            var file = new FileInfo(filepath);

            if (string.Compare(file.FullName, filepath, StringComparison.OrdinalIgnoreCase) != 0)
            {
                logger.LogWarning("file  from url not match file on disk. \r\nfile from url:\r\n'{}'\r\nfile from disk:\r\n'{1}'", filepath, file.FullName);
                return Results.NotFound();
            }

            return Results.File(await File.ReadAllBytesAsync(filepath), HttpContenTypeMaps.GetFromPathOrFallback(filepath));
        }

        static void GetSiteMap()
        {
            
        }


        static async Task<IResult> GetSingletonProjectSiteHtml(
            [FromServices] IQRepository repository,
            [FromRoute] string urlPrefix, string slug)
        {
            if (string.IsNullOrWhiteSpace(urlPrefix)) return Results.NotFound();
            if (string.IsNullOrWhiteSpace(slug)) return Results.NotFound();

            Project project = await repository.SelectSingletonProjectAsync(urlPrefix);

            if (project == null) return Results.NotFound();

            return await ReturnSiteItem(repository, project.Id, slug);
        }

        static async Task<IResult> GetVersionProjectSiteHtml([FromRoute] string urlPrefix, [FromRoute] string versionTag, string slug)
        {
            return Results.Content("not implemented", "text/plain");
        }

        [VCacheControl(CacheType = CacheControlType.Public, MaxAge = 30 * 60)]
        static async Task<IResult> GetNugetProjectSiteHtml(
            [FromServices] IOptions<DSettings> dsettings,
            [FromServices] IQRepository repository,
            [FromRoute] string nugetPackageName,
            [FromRoute] string nugetPackageVersion,
            string slug)
        {
            // todo when case invalid (e.g. AUTOmapper instead of AutoMapper) do redirect to valid case (must include {*slug});
            //      should be case sensitive
            // todo big in memory cache of: projects, shared_site_item
            // todo cache instead of db call
            if (slug == null) return Results.NotFound();

            Project project = await repository.SelectNugetProjectAsync(nugetPackageName, nugetPackageVersion);

            if (project == null) return Results.Redirect(dsettings.Value.GetUrlNugetProjectGenerate(nugetPackageName, nugetPackageVersion));

            return await ReturnSiteItem(repository, project.Id, slug);
        }

        private static async Task<IResult> ReturnSiteItem(IQRepository repository, long projectId, string slug)
        {
            var path = $"/{slug}";

            var siteItem = await repository.SelectSiteItemAsync(projectId, path);

            if (siteItem == null) return Results.NotFound();

            byte[] byteData = siteItem.ByteData;

            // todo logger.logcritical no sharedsiteitem found
            if (siteItem.SharedSiteItemId.HasValue) byteData = (await repository.SelectSharedSiteItem(siteItem.SharedSiteItemId.Value)).ByteData;

            if (siteItem.Path.EndsWith(".html", StringComparison.InvariantCultureIgnoreCase))
            {
                var html = Encoding.UTF8.GetString(byteData);
                return Results.Content(html, "text/html", Encoding.UTF8);
            }
            else
            {
                return Results.File(byteData, HttpContenTypeMaps.GetFromPathOrFallback(siteItem.Path));
            }
        }

        #region System pages

        public static async Task<IResult> SystemResourceMonitoring([FromServices] IQRepository repository)
        {
            IEnumerable<ResourceMonitorUtilization> utilization = await repository.SelectResourceMonitorUtilization(50);
            var sb = new StringBuilder();
            AppendHtmlTable(sb,
                new string[] { "id", "date_time", "cpu_used_percentage", "memory_userd_in_bytes", "memory_used_percentage" },
                new Func<ResourceMonitorUtilization, object>[]
                {
                    c => c.Id,
                    c => c.DateTime.ToString("yyyy-MM-dd T HH:mm:ss"),
                    c => Math.Round(c.CpuUsedPercentage, 2) + "%",
                    c => Math.Round(c.MemoryUsedInBytes/(double)1000000) + "MB",
                    c => Math.Round(c.MemoryUsedPercentage, 2) + "%"
                },
                utilization);

            int refreshRate = 5;

#if DEBUG
            refreshRate = 1;
#endif

            return SimpleHtmlPage($"<meta http-equiv=\"refresh\" content=\"{refreshRate}\">", sb);
        }

        public static async Task<IResult> SystemStats(
            [FromServices] IQRepository repository)
        {

            var s = await repository.SelectSystemStats();
            var sb = new StringBuilder();
            sb.AppendFormat("SiteItemCount: {0}", s.SiteItemCount);
            sb.AppendLine();
            sb.AppendFormat("SharedSiteItemCount: {0}", s.SharedSiteItemCount);
            sb.AppendLine();


            var sharedUsagePercent = s.SiteItemCountUsingShared == 0 ?
                "0" :
                Math.Round((double)100 * s.SiteItemCountUsingShared / s.SiteItemCount, 2).ToString();

            sb.AppendFormat("SiteItemCountUsingShared: {0} ({1}%)", s.SharedSiteItemCount, sharedUsagePercent);
            sb.AppendLine();
            sb.AppendFormat("AppLogCount: {0}", s.AppLogCount);
            sb.AppendLine();
            sb.AppendFormat("HttpLogCount: {0}", s.HttpLogCount);
            sb.AppendLine();
            sb.AppendFormat("ProjectCount: {0}", s.ProjectCount);
            sb.AppendLine();

            return Results.Content(sb.ToString(), "text/plain");
        }

        public static async Task<IResult> SystemSiteItems(
            [FromServices] IOptions<DSettings> settings,
            [FromServices] IQRepository repository,
            [FromRoute] int? pageNo)
        {
            var siteitems = await repository.GetSiteItemPagedAsync(0, 1000000);
            var allprojs = await repository.SelectProjectPagedAsync(0, 100000);
            var pdics = allprojs.ToImmutableDictionary(x => x.Id);

            var sb = new StringBuilder();
            sb.Append("<head></head><body><table>");
            sb.Append("<thead><tr> <td>SiteItemId</td>  <td>ProjectID</td> <td>Url</td> </tr></thead>");
            foreach (var si in siteitems)
            {
                sb.Append("<tr>");
                sb.AppendFormat("<td>{0}</td>", si.Id);
                sb.AppendFormat("<td>{0}</td>", si.ProjectId);
                sb.Append("<td>");
                sb.AppendFormat("<a href=\"{0}\">{0}</a>", FullProjectUrl(settings.Value, pdics[si.ProjectId], si.Path));
                sb.Append("</td>");
                sb.Append("</tr>");
            }
            sb.Append("</table></body>");

            var result = sb.ToString();
            return Results.Content(result, "text/html");
        }

        public static async Task<IResult> SystemAllProjects(
            [FromServices] IOptions<DSettings> settings,
            [FromServices] IQRepository repository,
            [FromRoute] int? pageNo)
        {
            var projects = await repository.SelectProjectPagedAsync(pageNo ?? 0, 1000);
            if (projects.Length == 0) return Results.Content("no projects", "text/html");

            var sb = new StringBuilder();


            var td = new Func<Project, string>[]
            {
                (Project p) => p.Id.ToString(),
                (Project p) => p.DnProjectId.ToString(),
                (Project p) => p.ProjectType.ToString(),
                (Project p) => p.NugetPackageName ?? "",
                (Project p) => p.NugetPackageVersion ?? "",
                (Project p) => p.UrlPrefix ?? "",
                (Project p) => p.ProjectVersion ?? "",
                (Project p) => FullProjectUrl(settings.Value, p)
            };

            int[] tabs = new int[td.Length];

            for (int i = 0; i < td.Length; i++)
            {
                tabs[i] = projects.Max(p => (int)Math.Ceiling((double)td[i](p).Length / 4));
            }

            sb.Append("<head></head><body>");
            sb.Append("<table>");
            foreach (var p in projects)
            {
                // sb.Append("|");
                sb.Append("<tr>");
                for (int i = 0; i < td.Length; i++)
                {
                    var value = td[i](p);
                    // var spaces = 4 * tabs[i] - value.Length;
                    // sb.Append($" {value}{new string(' ', spaces)} |");
                    if (i != td.Length - 1) { sb.Append("<td>"); sb.Append(value); sb.Append("</td>"); }
                    else { sb.Append("<td>"); sb.Append($"<a href=\"{value}\">{value}</a>"); sb.Append("</td>"); }
                }
                sb.Append("<tr>");



                // sb.AppendLine();
            }
            sb.Append("<table>");
            sb.Append("</body>");

            return Results.Content(sb.ToString(), "text/html");
        }


        static string FullProjectUrl(DSettings s, Project p, string path = "/api/index.html")
        {
            if (p.ProjectType == ProjectType.Nuget) return s.GetUrlNugetOrgProject(p.NugetPackageName, p.NugetPackageVersion, path);
            else if (p.ProjectType == ProjectType.Singleton) return s.GetUrlSingletonProject(p.UrlPrefix, path);
            else return s.GetUrlVersionProject(p.UrlPrefix, p.ProjectVersion, path);
        }

        #endregion

        static IResult SimpleHtmlPage(string headTags, StringBuilder body)
        {
            StringBuilder sb = new StringBuilder();
            string headFormat =
            """
<html>
 <head>
 {0}

 </head>
""";
            string bodyFormat =
@"
<body>
 <style>
 table, th, td {{
 border: 1px solid black;
 }}
td {{
padding: 8 12px;
}}
 table {{
 border-collapse: collapse;
 }}
 </style>

 {0}
 </body>
<html>
";
            sb.AppendFormat(headFormat, headTags ?? "");
            sb.AppendFormat(bodyFormat, body);

            return Results.Content(sb.ToString(), "text/html");
        }

        static void AppendHtmlTable<T>(StringBuilder sb, string[] columns, Func<T, object>[] config, IEnumerable<T> values)
        {
            if (columns.Length != config.Length) throw new ArgumentException("config.lenth != columns.length");

            sb.Append("<table>");
            sb.Append("<thead><tr>");

            foreach (var item in columns)
            {
                sb.AppendFormat("<td>{0}</td>", item);
            }

            sb.Append("</tr></thead><tbody>");

            foreach (var item in values)
            {
                sb.Append("<tr>");
                for (int i = 0; i < columns.Length; i++)
                {
                    sb.AppendFormat("<td>{0}</td>", config[i](item));
                }
                sb.Append("</tr>");
            }

            sb.Append("</tbody></table>");
        }
    }
}