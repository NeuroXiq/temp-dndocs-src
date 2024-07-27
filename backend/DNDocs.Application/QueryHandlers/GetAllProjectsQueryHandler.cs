using DNDocs.Application.Queries;
using DNDocs.Application.Shared;
using DNDocs.Domain.Entity.App;
using DNDocs.Domain.UnitOfWork;
using DNDocs.API.Model.DTO;
using DNDocs.API.Model.DTO.ProjectManage;

namespace DNDocs.Application.QueryHandlers
{
    internal class GetAllProjectsQueryHandler : QueryHandler<GetAllProjectsQuery, IList<ProjectDto>>
    {
        private IAppUnitOfWork appUow;

        public GetAllProjectsQueryHandler(IAppUnitOfWork appUow)
        {
            this.appUow = appUow;
        }

        protected override IList<ProjectDto> Handle(GetAllProjectsQuery query)
        {
            var all = appUow.GetSimpleRepository<Project>()
                .GetAll();

            return all.Select(p => Mapper.Map(p)).ToList();
        }
    }
}
