using DNDocs.API.Model.DTO.Enums;
using DNDocs.API.Model.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DNDocs.API.Model.DTO.ProjectManage
{
    public class ProjectVersioningInfoDto
    {
        public string GitTagName { get; set; }
        public int? ProjectId { get; set; }
        public IList<NugetPackageDto> ProjectNugetPackages { get; set; }

        public ProjectVersioningInfoDto() { }

        public ProjectVersioningInfoDto(string gitTag, int? projectId, IList<NugetPackageDto> projectNugetPackages)
        {
            GitTagName = gitTag;
            ProjectId = projectId;
            ProjectNugetPackages = projectNugetPackages;
        }
    }
}
