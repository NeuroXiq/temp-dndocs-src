using Microsoft.Extensions.Logging;
using DNDocs.Shared.Log;
using DNDocs.Domain.Utils;
using DNDocs.Domain.Utils.Docfx;
using DNDocs.Domain.ValueTypes;

namespace DNDocs.Infrastructure.Utils
{
    public interface IRobiniaInfrastructure
    {
        void RunAppMigrations();
        string GetOSPathGitRepoStoreRepo(Guid repoid);
    }


    internal class RobiniaInfrastructure : IRobiniaInfrastructure
    {
        private IServiceProvider serviceProvider;
        private IDocfxManager docfxManager;
        private ISystemProcess systemProcess;
        private ILogger<RobiniaInfrastructure> logger;

        public RobiniaInfrastructure(
            ILogger<RobiniaInfrastructure> logger,
            IServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider;
            this.logger = logger;
        }

        static string FID(Guid guid)
        {
            if (Guid.Empty == guid) throw new ArgumentException("Empty guid not allowed to use in infrastucture (safe guard)");

            return guid.ToString().ToUpper();
        }

        public string GetOSPathGitRepoStoreRepo(Guid repoid) => Path.Combine(RawRobiniaInfrastructure.GitStoreFullPath, FID(repoid));

        public void RunAppMigrations()
        {
            var cs = RawRobiniaInfrastructure.AppDatabaseConnectionString();
            Migrations.Run(cs);
        }
    }
}
