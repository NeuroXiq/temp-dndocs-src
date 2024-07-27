using DNDocs.Domain.Entity.App;

namespace DNDocs.Domain.Repository
{
    public interface IBgJobRepository : IRepository<BgJob>
    {
        public Task<BgJob> GetLatestBuildsProjectIdAsync(int projectId);
    }
}
