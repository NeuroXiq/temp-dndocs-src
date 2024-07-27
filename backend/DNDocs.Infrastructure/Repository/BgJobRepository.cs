using Microsoft.EntityFrameworkCore;
using DNDocs.Domain.Entity.App;
using DNDocs.Domain.Enums;
using DNDocs.Domain.Repository;
using System.Diagnostics;

namespace DNDocs.Infrastructure.Repository
{
    internal class BgJobRepository : BaseRepository<BgJob>, IBgJobRepository
    {
        public BgJobRepository(DbContext dbcontext) : base(dbcontext)
        {
        }

        // public async Task<BgJobStats> GetBgJobTimeStats()


        public async Task<BgJob> GetLatestBuildsProjectIdAsync(int projectId)
        {
            return await dbset.Where(t => t.BuildsProjectId == projectId)
                .OrderByDescending(t => t.QueuedDateTime)
                .FirstOrDefaultAsync();
        }
    }
}
