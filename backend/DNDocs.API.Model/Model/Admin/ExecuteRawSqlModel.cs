using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DNDocs.API.Model.Admin
{
    public class ExecuteRawSqlModel
    {
        public DTO.Enum.RawSqlExecuteMode Mode { get; set; }
        public string? DbName { get; set; }
        public string? SqlCode { get; set; }
    }
}
