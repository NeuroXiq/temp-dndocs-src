using DNDocs.Domain.Service;
using DNDocs.Domain.ValueTypes;

namespace DNDocs.Domain.Utils
{
    public interface IAppManager
    {
        IOSTempFolder CreateTempFolder();

        ExecuteRawSqlResult ExecuteRawSql(string dbname, int mode, string sqlcode);
        string GetOSPathGitRepoStoreRepo(Guid uuid);
        IGit OpenGitRepo(string repoUrl);
        bool GitRepoExistsURL(string repoUrl);
    }
}
