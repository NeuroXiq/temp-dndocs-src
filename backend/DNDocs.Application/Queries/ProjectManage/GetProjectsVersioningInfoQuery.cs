using DNDocs.Application.Shared;
using DNDocs.API.Model.DTO.ProjectManage;
using DNDocs.API.Model.DTO.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DNDocs.Application.Queries.ProjectManage
{
    public class GetProjectsVersioningInfoQuery : Query<TableDataDto<ProjectVersioningInfoDto>>
    {
        public int ProjectVersioningId { get; set; }
        public int PageNo { get; set; }
    }
}
