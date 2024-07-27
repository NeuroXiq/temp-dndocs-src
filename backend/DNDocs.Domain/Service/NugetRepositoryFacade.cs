using DNDocs.Domain.ValueTypes;

namespace DNDocs.Domain.Services
{
    public interface INugetRepositoryFacade
    {
        PackageLibFile[] FetchDllAndXmlFromPackage(string packageName, string version);
        PackageSearchMetadata GetLatestPackage(string packageName);
        PackageSearchMetadata GetPackageMetadata(string identityId, string identityVersion);
        PackageSearchMetadata[] GetPackageMetadata(string packageName);
    }

    public class PackageLibFile
    {
        public string FileName { get; set; }
        public string PackageRelativePath { get; set; }
        public byte[] ByteData { get; set; }
    }
}
