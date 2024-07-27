using Dapper;
using DNDocs.Docs.Web.Infrastructure;
using DNDocs.Docs.Web.Model;
using System.Collections.Concurrent;
using Vinca.Http.Logs;
using Vinca.TickLogger;

namespace DNDocs.Docs.Web.Service
{
    public interface ILogsService
    {
        void Flush();
        void TickLoggerSaveLogs(SaveLogsData d);
        void OnVHttpLogSaveLogs(VHttpLog[] logs);
    }

    public class LogsService : ILogsService
    {
        private IDInfrastructure infrastructure;
        private ILogger<LogsService> logger;
        private IServiceProvider serviceProvider;
        private IVTickLoggerService vTickLoggerService;

        public LogsService(
            IDInfrastructure infrastructure,
            IServiceProvider serviceProvider,
            IVTickLoggerService vTickLoggerService,
            ILogger<LogsService> logger)
        {
            this.infrastructure = infrastructure;
            this.logger = logger;
            this.serviceProvider = serviceProvider;
            this.vTickLoggerService = vTickLoggerService;
        }

        public void Flush()
        {
            this.vTickLoggerService.Flush();
            serviceProvider.GetRequiredService<IVHttpLogService>().Flush();
        }

        public void OnVHttpLogSaveLogs(VHttpLog[] logs)
        {
            Task.Factory.StartNew(SaveHttpLogsAsync, logs);
        }

        public async Task SaveHttpLogsAsync(object logsObj)
        {
            var logs = logsObj as VHttpLog[];
            logger.LogTrace("SaveHttpLogsAsync, count: {0}", logs.Length);

            try
            {
                using var scope = serviceProvider.CreateScope();
                using var repository = scope.ServiceProvider.GetRequiredService<ITxRepository>();
                repository.BeginTransaction();

                await repository.InsertHttpLogAsync(logs);
                await repository.CommitAsync();
            }
            catch (Exception e)
            {
                logger.LogError(e, "failed to save http logs");
                throw;
            }
        }

        public void TickLoggerSaveLogs(SaveLogsData logsData)
        {
            logger.LogTrace("OnTickLoggerSaveLogs logs count: {0}", logsData.Logs.Length);
            Task.Factory.StartNew(TickLoggerSaveLogsAsync, logsData.Logs);
        }

        private async Task TickLoggerSaveLogsAsync(object logRowsObj)
        {
            var logRows = logRowsObj as LogRow[];
            using var scope = (serviceProvider as IServiceProvider).CreateScope();
            using var repository = scope.ServiceProvider.GetRequiredService<ITxRepository>();
            var appLogs = logRows.Select(AppLog.FromVLog).ToList();

            repository.BeginTransaction();
            await repository.InsertAppLogAsync(appLogs);
            await repository.CommitAsync();
        }
    }
}
