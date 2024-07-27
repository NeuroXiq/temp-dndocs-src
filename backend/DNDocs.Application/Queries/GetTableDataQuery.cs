using DNDocs.Application.Shared;
using DNDocs.API.Model.DTO.Admin;
using DNDocs.API.Model.DTO.Shared;

namespace DNDocs.Application.Queries
{
    public class GetTableDataQuery : Query<TableDataDto<object>>
    {
        public TableDataRequest TableDataRequest { get; set; }
        
        public GetTableDataQuery(TableDataRequest request)
        {
            TableDataRequest = request;
        }
    }
}
