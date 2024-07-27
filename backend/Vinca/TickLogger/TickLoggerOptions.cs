using Vinca.TickLogger;

namespace Vinca.TickLogger
{
    public class TickLoggerOptions
    {
        public TimeSpan TimerTickTimeSpan { get; set; }
        public int MaxLogsTreshold { get; set; }
        public Action<SaveLogsData> OnSaveLogs { get; set; }
    }
}
