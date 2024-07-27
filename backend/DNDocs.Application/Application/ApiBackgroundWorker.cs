using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using DNDocs.Application.CommandHandlers;
using DNDocs.Application.Commands.Admin;
using DNDocs.Application.Shared;
using DNDocs.Domain.Entity.App;
using DNDocs.Domain.UnitOfWork;
using DNDocs.Domain.Utils;
using DNDocs.Shared.Configuration;
using System.Diagnostics;
using DNDocs.Application.Services;
using DNDocs.Shared.Utils;
using System.Reflection;

namespace DNDocs.Application.Application
{
    public class ApiBackgroundWorker : IHostedService, IDisposable
    {
        static bool IsNormalRunning = false;
        static bool IsImportantRunning = false;

        static object _lock = new object();
        CancellationTokenSource cancellationTokenSource;
        private IBgJobQueue bgjobQueue;
        private readonly int SleepSecondsDoImportantWork;
        private readonly int SleepSecondsDoWork;
        private Timer timerImportantWork;
        private Timer timerWork;

        private IServiceProvider services;
        private ILogger<ApiBackgroundWorker> logger;

        public ApiBackgroundWorker(IServiceProvider services,
            ILogger<ApiBackgroundWorker> logger,
            IOptions<DNDocsSettings> robiniaSettings,
            IBgJobQueue bgjobQueue)
        {
            this.services = services;
            this.logger = logger;
            this.SleepSecondsDoImportantWork = robiniaSettings.Value.BackendBackgroundWorkerDoImportantWorkSleepSeconds;
            this.SleepSecondsDoWork = robiniaSettings.Value.BackendBackgroundWorkerDoWorkSleepSeconds;
            cancellationTokenSource = new CancellationTokenSource();
            this.bgjobQueue = bgjobQueue;
        }

        // todo how to avoid statis methods in this class?
        // can be solved by always creating 'using (var socpe = servie.createscope())'
        // but lots of scopes will be created because lots of logs
        public static void HttpLog(HttpLog log) => DoBackgroundWorkHandler.HttpLogs.Add(log);

        public void HttpLogs(IList<HttpLog> logs)
        {
            foreach (var log in logs) DoBackgroundWorkHandler.HttpLogs.Add(log);
        }

        public static void Log(AppLog log) => DoBackgroundWorkHandler.Logs.Add(log);

        public void Logs(IList<AppLog> logs)
        {
            foreach (var log in logs) logs.Add(log);
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Starting backend background service");

            timerImportantWork = new Timer(TimerTickImportantWork, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(SleepSecondsDoImportantWork));
            timerWork = new Timer(TimerTickWork, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(SleepSecondsDoWork));
            await bgjobQueue.OnSystemStart();
        }

        private bool isStopping = false;

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if (!isDisposed)
            {
                Dispose(true);
                logger.Log(LogLevel.Information, "On before stopping backend background service");
                DoWork(WorkType.SystemImportant);

                cancellationTokenSource.Cancel();
            }

            // wait max 10 seconds and shutdown anyway (even if something is running)
            //for (int i = 0; i < 100 && IsAnythingRunning; i++) Thread.Sleep(100);

            //logger.Log(LogLevel.Information, "on after stopping backend background service");
            //if (IsAnythingRunning)
            //    logger.LogCritical("Something is running in background but force shutdown anyway. Not sure what to do with this for now. ");

            //return Task.CompletedTask;
        }

        DoBackgroundWorkCommand doBackgroundWorkCommand = null;

        public void DoSystemWorkNow(DoBackgroundWorkCommand command = null)
        {
            doBackgroundWorkCommand = command;
            TimerTickWork(null);
        }

        private void TimerTickImportantWork(object state)
        {
            DoWork(WorkType.SystemImportant);
        }

        private void TimerTickWork(object state)
        {
            DoWork(WorkType.SystemNormal);
        }

        enum WorkType
        {
            SystemNormal,
            SystemImportant
        }

        void DoWork(WorkType type)
        {
            lock (_lock)
            {
                if (type == WorkType.SystemImportant && IsImportantRunning) return;
                if (type == WorkType.SystemNormal && IsNormalRunning) return;

                if (type == WorkType.SystemImportant) IsImportantRunning = true;
                if (type == WorkType.SystemNormal) IsNormalRunning = true;
            }

            if (type == WorkType.SystemImportant)
            {

                // now this runs on Timer thread,
                // this is not correct way (timer operations should be very short, start thread or something and immediately return)
                // but for now it works so instead of creating new
                // thread everytime (saving logs should be short operation)
                // run directly on timer thread in 'incorrect' way
                // this should be invoked often because logs should be
                // save in db as fast as possible

                try
                {
                    using (var scope = services.CreateScope())
                    {
                        var h = scope.ServiceProvider.GetRequiredService<DoBackgroundWorkHandler>();
                        var cu = scope.ServiceProvider.GetRequiredService<ICurrentUser>();

                        // to skip logging and storing trace logs in db
                        // (very ofter calling handler, so logs of logs that are not needed);
                        h.DoImportantWork();
                    }

                    IsImportantRunning = false;
                }
                catch (Exception e)
                {
                    IsImportantRunning = false;
                    logger.LogCritical(e, "system important exception");

                    // question: what should  happen in this unhandled  exception in background worker service?
                    // throw;
                }

                return;
            }

            // todo: more therads
            try
            {
                var t = new Thread(ThreadHandlerWork);

                t.Priority = ThreadPriority.Normal;
                t.IsBackground = true;
                t.Start();
            }
            catch (Exception)
            {
                if (type == WorkType.SystemNormal) IsNormalRunning = false;

                // question: what should  happen in this unhandled  exception in background worker service?
                //throw;
            }
        }

        void ThreadHandlerWork()
        {
            try
            {
                while (bgjobQueue.TryDequeue(out var item))
                {
                    using (var scope = services.CreateScope())
                    {
                        var uow = scope.ServiceProvider.GetRequiredService<IAppUnitOfWork>();
                        BgJob job = null;

                        try
                        {
                            var cd = scope.ServiceProvider.GetRequiredService<ICommandDispatcher>();
                            var currentUser = scope.ServiceProvider.GetRequiredService<ICurrentUser>();

                            var bgjobRepo = scope.ServiceProvider.GetRequiredService<IAppUnitOfWork>().BgJobRepository;
                            job = bgjobRepo.GetByIdChecked(item.BgJobId);

                            currentUser.AuthenticateAsUser(fromUserId: item.UserId);

                            job.SetInProgress(Thread.CurrentThread.ManagedThreadId.ToString());
                            uow.SaveChanges();

                            var result = cd.Dispatch(item.Command, cancellationTokenSource.Token);

                            job.SetCompleted(result.Success, JsonConvert.SerializeObject(result));
                        }
                        catch (Exception e)
                        {
                            logger.LogError(e, "failed processing job: {0}", item?.BgJobId);
                            job?.SetFailedToRun(Helpers.ExceptionToStringForLogs(e));
                        }
                        uow.SaveChanges();
                    }
                }
            }
            catch (Exception e)
            {
                this.logger.LogError(e, "failed to process bg job queue");
            }
            finally
            {
                IsNormalRunning = false;
            }
        }

        ~ApiBackgroundWorker() { Dispose(false); }
        public void Dispose() { Dispose(true); }
        private bool isDisposed = false;

        private void Dispose(bool disposing)
        {
            if (!isDisposed)
            {
                isDisposed = true;
                this.timerImportantWork?.Dispose();
                this.timerWork?.Dispose();
            }
        }
    }
}
