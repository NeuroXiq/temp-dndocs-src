using DNDocs.API.Model.DTO.Enum;

namespace DNDocs.API.Model.DTO
{
    public class QueryResultDto<TResult> : HandlerResultDto
    {
        public QueryResultDto() : base(true, null, null)
        {
        }

        public TResult Result { get; set; }
    }
}
