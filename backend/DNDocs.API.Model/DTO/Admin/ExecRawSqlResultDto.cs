using DNDocs.API.Model.DTO.Enum;

namespace DNDocs.API.Model.DTO.Admin
{
    public class ExecRawSqlResultDto
    {
        public string[] Columns { get; set; }
        public object[][] Rows { get; set; }

        public RawSqlExecuteMode ExecuteMode { get; set; }
        public int? ExecuteNonQueryResult { get; set; }
        public bool Success { get; set; }
        public string Exception { get; set; }
    }
}
