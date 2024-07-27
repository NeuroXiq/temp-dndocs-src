using DNDocs.Application.Shared;
using DNDocs.API.Model.DTO.Admin;
using DNDocs.API.Model.DTO.Enum;

namespace DNDocs.Application.Queries.Admin
{
    public class ExecuteRawSqlQuery : Query<ExecRawSqlResultDto>
    {
        public string DbName { get; set; }
        public RawSqlExecuteMode Mode { get; set; }
        public string SqlCode { get; set; }

        public ExecuteRawSqlQuery(string dbname, RawSqlExecuteMode mode, string sqlCode)
        {
            DbName = dbname;
            Mode = mode;
            SqlCode = sqlCode;
        }
    }
}
