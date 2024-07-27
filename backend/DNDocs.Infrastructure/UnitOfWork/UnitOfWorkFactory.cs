using Microsoft.Extensions.DependencyInjection;
using DNDocs.Domain.UnitOfWork;
using DNDocs.Domain.ValueTypes;
using DNDocs.Infrastructure.DataContext;
using DNDocs.Infrastructure.Utils;

namespace DNDocs.Infrastructure.UnitOfWork
{
    internal class UnitOfWorkFactory : IUnitOfWorkFactory
    {
        private IRobiniaInfrastructure robiniaInfrastructure;
        private IServiceProvider serviceProvider;

        public UnitOfWorkFactory(IServiceProvider serviceProvider,
            IRobiniaInfrastructure robiniaInfrastructure)
        {
            this.robiniaInfrastructure = robiniaInfrastructure;
            this.serviceProvider = serviceProvider;
        }

        public IAppUnitOfWork GetAppUnitOfWork() => new AppUnitOfWork(serviceProvider.GetRequiredService<AppDbContext>());
    }
}
