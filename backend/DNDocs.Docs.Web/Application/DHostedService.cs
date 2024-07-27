using DNDocs.Docs.Web.Model;
using DNDocs.Docs.Web.Service;
using DNDocs.Docs.Web.Shared;
using Microsoft.Extensions.Diagnostics.ResourceMonitoring;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using System.Diagnostics;
using System.Text;
using Vinca.Http.Logs;
using Vinca.SitemapXml;
using Vinca.Utils;

namespace DNDocs.Docs.Web.Application
{
    public class DHostedService : IHostedService
    {
        private Timer logsTimer;
        private Timer resourceMonitorTimer;
        private Timer systemWorkTimer;
        private DSettings settings;
        private IServiceProvider serviceProvider;
        private ILogsService logsService;
        private ILogger<DHostedService> logger;
        private IResourceMonitor resourceMonitor;
        private Thread systemWorkThread;
        private int isSystemThreadRun;

        public DHostedService(
            IServiceProvider serviceProvider,
            ILogsService logsService,
            IResourceMonitor resourceMonitor,
            ILogger<DHostedService> logger,
            IOptions<DSettings> settings)
        {
            this.settings = settings.Value;
            this.serviceProvider = serviceProvider;
            this.logsService = logsService;
            this.logger = logger;
            this.resourceMonitor = resourceMonitor;

            isSystemThreadRun = 0;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine("started");

            logsTimer = new Timer(LogsTimerCallback, null, settings.FlushAllLogsTimeSpan, settings.FlushAllLogsTimeSpan);
            resourceMonitorTimer = new Timer(ResourceMonitorTick, null, TimeSpan.Zero, settings.SaveResourceMonitorUtilizationTimeSpan);
            systemWorkTimer = new Timer(OnSysTimerTick, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10 * 60 * 1000));
            // systemWorkTimer = new Timer(DoSystemWorkTimerCallback, null, 5, 2000 );
            // throw new NotImplementedException();
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            // todo everything must have cancellationtoken to stop in conrolled/success way
            this.logsService.Flush();
            await Task.Delay(5000);
            // throw new NotImplementedException();
        }


        void ResourceMonitorTick(object _)
        {
            Task.Factory.StartNew(() =>
            {
                TransactionWrap(async (sp, repository) =>
                {
                    // var u = sp.GetRequiredService<IResourceMonitor>().GetUtilization(settings.SaveResourceMonitorUtilizationTimeSpan);
                    // var rmu = new ResourceMonitorUtilization(u.CpuUsedPercentage, u.MemoryUsedInBytes, u.MemoryUsedPercentage, DateTime.UtcNow);
                    // await repository.InsertResourceMonitorUtilization(rmu);
                }).Wait();
            });
        }

        void LogsTimerCallback(object _)
        {
            var httpLogsService = this.serviceProvider.GetRequiredService<ILogsService>();
            httpLogsService.Flush();
        }

        void DoWork()
        {
            // work to do:
            // 1. ccheck sector sizes
            // 2. to delete projects remove
            // 3. backup azure
            // 4. generate sitemaps
            // 5. check problems: project is in app.sqlite not in site.sqlite
            // 6.  pragma wal_checkpoint
        }

        private void OnSysTimerTick(object state)
        {
            logger.LogTrace("starting do system work");
            var isRunning = Interlocked.Exchange(ref isSystemThreadRun, 1);

            if (isRunning != 0) return;

            try
            {
                systemWorkThread = new Thread(DoSystemWorkThreadStart);
                systemWorkThread.IsBackground = false;

                systemWorkThread.Start();
            }
            catch (Exception e)
            {
                isRunning = 0;
                logger.LogError(e, "failed to start system background work thread");
            }
        }

        private void DoSystemWorkThreadStart()
        {
            var jobs = new[]
            {
                () => TransactionWrap(GenerateSitemaps),
                DbCleanup,
            };

            foreach (var job in jobs)
            {
                try
                {
                    job().Wait();
                }
                catch (Exception e)
                {
                    logger.LogCritical(e, "exception during background system thread");
                }
            }
            

            isSystemThreadRun = 0;
        }

        private async Task TransactionWrap(Func<IServiceProvider, ITxRepository, Task> action)
        {
            using var scope = serviceProvider.CreateScope();
            using var repository = scope.ServiceProvider.GetRequiredService<ITxRepository>();

            try
            {
                repository.BeginTransaction();
                await action(scope.ServiceProvider, repository);
                await repository.CommitAsync();
            }
            catch (Exception)
            {

                throw;
            }
            finally
            {

                repository.Dispose();
            }
        }

        private async Task DbCleanup()
        {
            //vacuum: 100% disk usage  on two 2GB databases
            // 1. is this needed to vacuum? on the internet said that 
            // 2. is checkpoint needed?
            // actually maybe if will be fine without this, as not too much
            // deletes are expected to be
            //
            // this is just optimalization not 
            //using var appConnection = infrastructure.CreateSqliteConnection(DatabaseType.App);
            //using var siteConnection = infrastructure.CreateSqliteConnection(DatabaseType.Site);
            //using var logConnection = infrastructure.CreateSqliteConnection(DatabaseType.Log);

            //await appConnection.ExecuteAsync("VACUUM");
            //await siteConnection.ExecuteAsync("VACUUM");
            //await logConnection.ExecuteAsync("VACUUM");

            //using var connLogs = CreateLogDbConnection;
            //var sql = $"delete from resource_monitor_utilization WHERE date_time < @Deprecated";
            //await connLogs.ExecuteAsync(sql, new { Deprecated = DateTime.UtcNow.AddDays(-7) });
            //await connLogs.ExecuteAsync("PRAGMA wal_checkpoint;");
            //await connLogs.ExecuteAsync("VACUUM");

            //using var connApp = CreateAppDbConnection;
            //await connApp.ExecuteAsync("PRAGMA wal_checkpoint;");
            //await connApp.ExecuteAsync("VACUUM");

            //using var connSite = CreateSiteDbConnection;
            //await connSite.ExecuteAsync("PRAGMA wal_checkpoint;");
            //await connSite.ExecuteAsync("VACUUM");
        }

        private async Task GenerateSitemaps(IServiceProvider scope, ITxRepository repository)
        {
            var sw = Stopwatch.StartNew();
            logger.LogInformation("starting to regenerate sitemaps");
            SitemapGenerator sitemapGenerator = new SitemapGenerator();
            var needSitemapId = await repository.ScriptForSitemapGenerator();
            
            foreach (var projectId in needSitemapId)
            {
                // await repository.CommitAsync();
                // repository.BeginTransaction();

                var p = await repository.SelectProjectByIdAsync(projectId);
                IList<string> urls = (await repository.SelectSiteItemPathByProjectId(projectId)).ToList();

                switch (p.ProjectType)
                {
                    case ProjectType.Singleton: urls = urls.Select(u => settings.GetUrlSingletonProject(p.UrlPrefix, u)).ToList(); break;
                    case ProjectType.Version:   urls = urls.Select(u => settings.GetUrlVersionProject(p.UrlPrefix, p.ProjectVersion, u)).ToList(); break;
                    case ProjectType.Nuget:     urls = urls.Select(u => settings.GetUrlNugetOrgProject(p.NugetPackageName, p.NugetPackageVersion, u)).ToList(); break;
                    default: throw new NotImplementedException();
                }

                var now = DateTime.UtcNow;

                if (sitemapGenerator.CanAppend(urls, DateTime.UtcNow, ChangeFreq.Monthly))
                {
                    sitemapGenerator.Append(urls, now, ChangeFreq.Monthly);
                    continue;
                }
                else if (sitemapGenerator.UrlsCount == 0)
                {
                    logger.LogError("failed to generate sitemap for projectid: {0}, skipping this project without sitemap", projectId);
                    continue;
                }
                else
                {
                    var sitemapXmlString = sitemapGenerator.ToXmlStringAndClear();
                    var sitemap = new Sitemap()
                    {
                        CreatedOn = DateTime.Now,
                        UpdatedOn = DateTime.Now,
                        SitemapName = $"sitemap_project_{Guid.NewGuid()}.xml",
                        ByteData = Encoding.UTF8.GetBytes(sitemapXmlString)
                    };

                    await repository.InsertSitemap(sitemap);
                }
            }

            var allSitemaps = await repository.SelectSitemap();
            var sitemapIndexGen = new SitemapIndexGenerator();
            var allUrls = allSitemaps.Select(t => t.SitemapName).ToList();

            // todo datetime.utcnow even if some not changed - do something with this?
            sitemapIndexGen.Append(allUrls, DateTime.UtcNow);

            var result = sitemapIndexGen.ToStringXmlAndClear();
            var sitemapIndex = new Sitemap()
            {
                CreatedOn = DateTime.Now,
                UpdatedOn = DateTime.Now,
                SitemapName = "sitemapindex.xml",
                ByteData = Encoding.UTF8.GetBytes(result)
            };

            await repository.InsertSitemap(sitemapIndex);

            sw.Stop();

            logger.LogInformation("completed generating sitemaps, project_id:({0}) total time: {1}s",
                needSitemapId.StringJoin(",", t => t.ToString()), sw.Elapsed.Seconds);
        }
    }
}
