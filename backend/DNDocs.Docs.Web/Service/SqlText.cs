namespace DNDocs.Docs.Web.Service
{
    public class SqlText
    {
        public const string SelectProject = @" SELECT
                    id AS Id, dn_project_id as DnProjectId, metadata as Metadata, url_prefix as UrlPrefix, project_version as ProjectVersion, 
                    nuget_package_name as NugetPackageName, nuget_package_version as NugetPackageVersion, project_type as ProjectType, 
                    created_on as CreatedOn, updated_on as UpdatedOn 
                    FROM project";

        public const string SelectSharedSiteItem =
@"SELECT 
id as Id, path as Path, byte_data as ByteData, sha_256 as Sha256
FROM shared_site_item
";

    }
}
