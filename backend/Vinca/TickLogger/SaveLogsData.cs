using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vinca.TickLogger
{
    public struct SaveLogsData
    {
        public LogRow[] Logs;
        public IServiceProvider ServiceProvider;
    }
}
