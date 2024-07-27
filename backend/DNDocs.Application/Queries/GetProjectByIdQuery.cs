using DNDocs.Application.Shared;
using DNDocs.API.Model.DTO;
using DNDocs.API.Model.DTO.ProjectManage;

namespace DNDocs.Application.Queries
{
    public class GetProjectByIdQuery : Query<ProjectDto>
    {
        public int Id { get; set; }

        public GetProjectByIdQuery() { }

        public GetProjectByIdQuery(int id)
        {
            Id = id;
        }
    }
}
