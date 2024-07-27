namespace DNDocs.Domain.Service
{
    public interface IGit : IDisposable
    {
        string RepoOSPath { get; }
        string NewestCommitHashForPath(string dirInRepoRelativePath);
        void InitInstance(string repoUrl);
        int GetCommitNoFromHEAD(string commit);
        void Pull();
        void FetchAll();
        void PruneRemote();
        bool BranchExists(string branchName);
        void CheckoutBranch(string branchName);
        void CheckoutCommit(string commit);
        bool RepositoryExistsURL(string url);
        bool TagExists(string gitTagName);
        string[] GetAllTags();
        string GetTagCommitHash(string gitTagName);
    }
}
