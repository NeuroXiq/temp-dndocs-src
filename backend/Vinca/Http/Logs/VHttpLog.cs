using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vinca.Http.Logs
{
    public class VHttpLog
    {
        public long Id { get; set; }
        public string Date { get; set; }
        public string Time { get; set; }
        public string ClientIP { get; set; }
        public int? ClientPort { get; set; }
        public string Method { get; set; }
        public string UriPath { get; set; }
        public string UriQuery { get; set; }
        public int ResponseStatus { get; set; }
        public long? BytesSend { get; set; }
        public long? BytesReceived { get; set; }
        public long? TimeTakenMs { get; set; }
        public string Host { get; set; }
        public string UserAgent { get; set; }
        public string Referer { get; set; }
        public int TestIndex1 { get; set; }
        public string TestIndex2 { get; set; }
    }
}
