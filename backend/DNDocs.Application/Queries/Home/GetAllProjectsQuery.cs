using DNDocs.Application.Shared;
using DNDocs.API.Model.DTO;
using DNDocs.API.Model.DTO.ProjectManage;

namespace DNDocs.Application.Queries.Home
{
    public class GetAllProjectsQuery : Query<IList<ProjectDto>>
    {
        public GetAllProjectsQuery()
        {
        }
    }
}
