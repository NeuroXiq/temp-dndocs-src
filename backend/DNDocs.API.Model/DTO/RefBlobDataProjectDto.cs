using DNDocs.API.Model.DTO.Enum;

namespace DNDocs.API.Model.DTO
{
    public class RefBlobDataProjectDto
    {
        public int BlobDataId { get; set; }
        public int ProjectId { get; set; }
        public ProjectFileType ProjectFileType { get; set; }
        public BlobDataDto BlobDataDto { get; set; }

        public RefBlobDataProjectDto() { }

        public RefBlobDataProjectDto(int blobdataid, int projectid, ProjectFileType filetype, BlobDataDto blobDataDto)
        {
            BlobDataId = blobdataid;
            ProjectId = projectid;
            ProjectFileType = filetype;
            BlobDataDto = blobDataDto;
        }
    }
}
