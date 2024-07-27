
using DNDocs.Application.Shared;
using DNDocs.Domain.UnitOfWork;
using DNDocs.API.Model.DTO;
using DNDocs.Application.Queries.Integration;
using DNDocs.API.Model.DTO.MyAccount;
using DNDocs.Domain.Entity.App;
using DNDocs.Infrastructure.Utils;
using Microsoft.EntityFrameworkCore;
using DNDocs.Application.CommandHandlers.Integration;
using DNDocs.Application.Application;
using DNDocs.Domain.Utils;
using Microsoft.Extensions.Options;
using DNDocs.Shared.Configuration;
using Newtonsoft.Json;
using DNDocs.Application.Services;

namespace DNDocs.Application.QueryHandlers.DocfxExplorer
{
    internal class GetNugetCreateProjectStatusHandler: QueryHandlerA<GetNugetCreateProjectStatusQuery, BgJobViewModel>
    {
        private DNDocsSettings settings;
        private IAppUnitOfWork appUow;
        private ICache cache;
        private ApiBackgroundWorker apiBackgroundWorker;
        private ApiBackgroundWorker abw;
        private IBgJobQueue bgjobQueue;

        public GetNugetCreateProjectStatusHandler(
            IAppUnitOfWork appUow,
            ICache cache,
            ApiBackgroundWorker apiBackgroundWorker,
            IOptions<DNDocsSettings> dsettings,
            ApiBackgroundWorker abw,
            IBgJobQueue bgjobQueue)
        {
            this.settings = dsettings.Value;
            this.appUow = appUow;
            this.cache = cache;
            this.apiBackgroundWorker = apiBackgroundWorker;
            this.abw = abw;
            this.bgjobQueue = bgjobQueue;
        }

        protected override async Task<BgJobViewModel> Handle(GetNugetCreateProjectStatusQuery query)
        {
            var project = await appUow.ProjectRepository.GetNugetOrgProjectAsync(query.PackageName, query.PackageVersion);

            if (project == null) return null;

            var bgjob = await appUow.BgJobRepository.GetLatestBuildsProjectIdAsync(project.Id);

            if (bgjob == null) return null;

            double estExeTime = await cache.GetOrAddOKMAsync<double>(this, "job_exetime", () => bgjobQueue.GetJobsExecutionEstimates(), TimeSpan.FromSeconds(30));
            int countBefore = await cache.GetOrAddOKMAsync<int>(this, $"jobs_before_{bgjob.Id}", () => bgjobQueue.GetJobsCountInQueueBeforeJob(bgjob.Id), TimeSpan.FromSeconds(5)); 

            var result = new BgJobViewModel
            {
                EsitamedTimeWillExecuteSeconds = (int)estExeTime,
                EstimatedTimeToStartSeconds = (int)(estExeTime * countBefore),
                EstimateOtherJobsBeforeThis = countBefore,

                BgJobId = bgjob.Id,
                BgJobStatus = (API.Model.DTO.Enum.BgJobStatus)bgjob.Status,
                CommandHandlerSuccess = bgjob.CommandHandlerSuccess,
                ProjectApiFolderUrl = settings.GetUrlNugetOrgProject(project.NugetOrgPackageName, project.NugetOrgPackageVersion),
                CommandHandlerErrorMessage = bgjob.CommandHandlerResult != null ? JsonConvert.DeserializeObject<HandlerResult>(bgjob.CommandHandlerResult)?.ErrorMessage : null
            };

            abw.DoSystemWorkNow();

            return result;
        }
    }
}
