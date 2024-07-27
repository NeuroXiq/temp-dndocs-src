using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vinca.Http.Logs
{
    public interface IVHttpLogService
    {
        bool ShouldSaveLog(HttpContext context);
        void SaveLog(VHttpLog log);
        void Flush();
    }

    internal class VHttpLogService : IVHttpLogService
    {
        private IServiceProvider serviceProvider;
        private VHttpLogOptions options;
        private ConcurrentBag<VHttpLog> mainQueue;
        private ConcurrentBag<VHttpLog> swapQueue;
        private int flushing;

        public VHttpLogService(IOptions<VHttpLogOptions> options, IServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider;
            this.options = options.Value;
            mainQueue = new ConcurrentBag<VHttpLog>();
            swapQueue = new ConcurrentBag<VHttpLog>();
            flushing = 0;
        }

        public void Flush()
        {
            int nowFlushing = Interlocked.Exchange(ref flushing, 0);
            if (nowFlushing != 0) return;

            var toSaveQueue = Interlocked.Exchange(ref mainQueue, swapQueue);

            var toSaveLogs = toSaveQueue.ToArray();

            try
            {
                // todo after safe should objectpool<vhttplog> somehow
                toSaveQueue.Clear();
                options.OnSaveLogs(serviceProvider, toSaveLogs);

            }
            catch (Exception e)
            {
                throw;
            }
            finally
            {
                // after swap just make current one to be swaped next
                flushing = 0;
                swapQueue = toSaveQueue;
            }
        }

        public void SaveLog(VHttpLog log)
        {
            mainQueue.Add(log);
            if (mainQueue.Count > options.MaxQueueSize) Flush();
        }

        public bool ShouldSaveLog(HttpContext context)
        {
            return options.ShouldSaveLog == null ? true : options.ShouldSaveLog(context);
        }
    }

    public class VHttpLogOptions
    {
        public int MaxQueueSize { get; set; }
        public Func<HttpContext, bool> ShouldSaveLog { get; set; }
        public Action<IServiceProvider, VHttpLog[]> OnSaveLogs { get; set; }
    }
}
