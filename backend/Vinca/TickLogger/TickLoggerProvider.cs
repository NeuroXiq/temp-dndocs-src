using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Runtime.Versioning;

namespace Vinca.TickLogger
{
    public interface IVTickLoggerService
    {
        void Flush();    
    }

    [UnsupportedOSPlatform("browser")]
    [ProviderAlias("TickLogger")]
    public sealed class TickLoggerProvider : ILoggerProvider, IVTickLoggerService
    {
        private TickLoggerOptions options;
        private IDisposable onChangeToken;
        private readonly IServiceProvider serviceProvider;
        private readonly ConcurrentDictionary<string, TickLogger> loggers;

        private System.Threading.Timer timer;
        private int timerTickExecuting;
        private object _lock = new object();
        private List<LogRow> logs;

        public TickLoggerProvider(
            IServiceProvider serviceProvider,
            IOptionsMonitor<TickLoggerOptions> options)
        {

            this.serviceProvider = serviceProvider;
            this.options = options.CurrentValue;
            onChangeToken = options.OnChange(updated => this.options = updated);
            loggers = new ConcurrentDictionary<string, TickLogger>();
            timerTickExecuting = 0;
            logs = new List<LogRow>();
        }

        public ILogger CreateLogger(string categoryName)
        {
            return loggers.GetOrAdd(categoryName, name => new TickLogger(name, GetCurrentOptions, AppendLog));
        }

        TickLoggerOptions GetCurrentOptions() => options;

        public void Dispose()
        {
            loggers.Clear();
            onChangeToken?.Dispose();
        }

        // todo object pool for logs, and pass all data as method parameter?
        private void AppendLog(LogRow row)
        {
            bool flush = false;
            lock (_lock)
            {
                logs.Add(row);

                flush = logs.Count > options.MaxLogsTreshold;
            }

            if (flush) TimerTick(null);
        }

        internal void StartTimer()
        {
            timer = new Timer(TimerTick, null, options.TimerTickTimeSpan, options.TimerTickTimeSpan);
        }

        void TimerTick(object _) => Flush();

        public void Flush()
        {
            try
            {
                int wasAlreadyLocked = Interlocked.Exchange(ref timerTickExecuting, 1);

                if (wasAlreadyLocked > 0) return;

                LogRow[] logsToSave = null;

                lock (_lock)
                {
                    if (logs.Count == 0) return;

                    logsToSave = logs.ToArray();
                    logs.Clear();
                }

                if (logsToSave.Length == 0) return;


                options.OnSaveLogs(new SaveLogsData { Logs = logsToSave, ServiceProvider = this.serviceProvider });
            }
            catch (Exception e)
            {
            }
            finally
            {
                timerTickExecuting = 0;
            }
        }
    }

    public static class TickLoggerExtensions
    {
        public static void AddVincaTickLogger(this IServiceCollection services, Action<TickLoggerOptions> configue)
        {
            services.Configure(configue);
            services.AddSingleton<IVTickLoggerService, TickLoggerProvider>();
            services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, TickLoggerProvider>());
        }

        /// <summary>
        /// Starts timer that sends logs periodically
        /// </summary>
        /// <param name="builder"></param>
        /// <exception cref="InvalidOperationException"></exception>
        public static void StartVincaTickLogger(this IApplicationBuilder builder)
        {
            // var a = builder.ApplicationServices.GetService<TickLoggerProvider>();
            var tickLoggerProvider = builder.ApplicationServices.GetServices<ILoggerProvider>().Where(t => t is TickLoggerProvider).FirstOrDefault() as TickLoggerProvider;

            if (tickLoggerProvider == null)
            {
                throw new InvalidOperationException("cannot resolve tickloggerprovider - not in the list of <ILoggerProvider> service. Is logger added to services?");
            }

            tickLoggerProvider.StartTimer();
        }
    }
}
