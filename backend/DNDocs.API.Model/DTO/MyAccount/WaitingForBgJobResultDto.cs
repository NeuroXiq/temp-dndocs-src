using DNDocs.API.Model.DTO.Enum;
using DNDocs.API.Model.DTO.ProjectManage;

namespace DNDocs.API.Model.DTO.MyAccount
{
    public class BgJobViewModel
    {
        public int EsitamedTimeWillExecuteSeconds { get; set; }
        public int EstimatedTimeToStartSeconds { get; set; }
        public int EstimateOtherJobsBeforeThis { get; set; }

        public int BgJobId { get; set; }
        public BgJobStatus BgJobStatus { get; set; }
        public bool? CommandHandlerSuccess { get; set; }
        public string CommandHandlerErrorMessage { get; set; }

        public ProjectDto CreatedProject { get; set; }
        public string ProjectApiFolderUrl { get; set; }
    }
}
