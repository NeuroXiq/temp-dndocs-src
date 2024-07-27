using Dapper;
using DbUp;
using DNDocs.Docs.Web.Shared;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using System.Data.SQLite;
using System.Diagnostics;

namespace DNDocs.Docs.Web.Infrastructure
{
    public interface IDInfrastructure
    {
        // public 
        void ApplicationStartup();
        SQLiteConnection CreateSqliteConnection(DatabaseType db);
        string AttachDatabase(DatabaseType type);
    }

    public enum DatabaseType
    {
        App,
        Site,
        Log,
        VarSite
    }

    public class DInfrastructure : IDInfrastructure
    {
        private DFileSystemOptions options;

        string ConnectionString_App;
        string ConnectionString_Site;
        string ConnectionString_VarSite;
        string ConnectionString_Log;

        public DInfrastructure(IOptions<DFileSystemOptions> fsOptions)
        {
            this.options = fsOptions.Value;
            if (!Directory.Exists(options.InfrastructureFolderOSPath))
                throw new ArgumentException($"(Safety): Directory for instrastructure not exists: '{options.InfrastructureFolderOSPath}'. Create this directory manually");

            ConnectionString_App = $"Data Source={Path.Combine(options.InfrastructureFolderOSPath, options.AppDbSqliteFileName)};";
            ConnectionString_Site = $"Data Source={Path.Combine(options.InfrastructureFolderOSPath, options.SiteDbSqliteFileName)};";
            ConnectionString_Log = $"Data Source={Path.Combine(options.InfrastructureFolderOSPath, options.LogDbSqliteFileName)};";
            ConnectionString_VarSite = $"Data Source={Path.Combine(options.InfrastructureFolderOSPath, options.VarSiteDbSqliteFileName)};";
        }

        public string AttachDatabase(DatabaseType type)
        {
            if (type != DatabaseType.App) throw new NotImplementedException("implement others");
            return $"ATTACH DATABASE '{Path.Combine(options.InfrastructureFolderOSPath, options.AppDbSqliteFileName)}'";
        }

        public SQLiteConnection CreateSqliteConnection(DatabaseType db)
        {
            string connectionString = null;
            switch (db)
            {
                case DatabaseType.App: connectionString = ConnectionString_App; break;
                case DatabaseType.Log: connectionString = ConnectionString_Log; break;
                case DatabaseType.Site: connectionString = ConnectionString_Site; break;
                case DatabaseType.VarSite: connectionString = ConnectionString_VarSite; break;
                default: throw new ArgumentException(nameof(db));
            }

            var sqliteConnection = new SQLiteConnection(connectionString);
            sqliteConnection.BusyTimeout = 5 * 60 * 1000;
            sqliteConnection.Execute("PRAGMA journal_size_limit=1000000000;"); //?
            return sqliteConnection;
        }

        public void ApplicationStartup()
        {
            CreateDbIfNotExists(ConnectionString_App);
            CreateDbIfNotExists(ConnectionString_Site);
            CreateDbIfNotExists(ConnectionString_VarSite);
            CreateDbIfNotExists(ConnectionString_Log);

            RunMigration(ConnectionString_App, "DNDocs.Docs.Web.Infrastructure.Migrations.App.");
            RunMigration(ConnectionString_Site, "DNDocs.Docs.Web.Infrastructure.Migrations.Site.");
            RunMigration(ConnectionString_VarSite, "DNDocs.Docs.Web.Infrastructure.Migrations.VarSite.");
            RunMigration(ConnectionString_Log, "DNDocs.Docs.Web.Infrastructure.Migrations.Log.");
        }

        static void CreateDbIfNotExists(string connStr)
        {
            using (var connection = new SQLiteConnection(connStr))
            {
                connection.Open();
                connection.Close();
            }
        }

        static void RunMigration(string connectionString, string filter)
        {
            //RawRobiniaInfrastructure.CreateSqliteDbIfNotExists(connectionString);
            var asm = typeof(DInfrastructure).Assembly;

            var upgrader = DeployChanges.To
                .SQLiteDatabase(connectionString)
                .WithScriptsEmbeddedInAssembly(asm, (resourceName) => resourceName.StartsWith(filter))
                .LogScriptOutput()
                // .WithPreprocessor(new TransactionProcessor())
                .LogToConsole()
                // .WithTransaction() <-- needed by 0008 migration
                .Build();

            var result = upgrader.PerformUpgrade();

            if (!result.Successful)
            {
                throw result.Error;
            }
        }
    }

    public static class InfrastructureExtensions
    {
        public static void AddDNDDInfrastructure(this IServiceCollection sc)
        {
            sc.AddSingleton<IDInfrastructure, DInfrastructure>();
        }

        public static void DInfrastructureAppBuilded(this WebApplication w)
        {
            var i = w.Services.GetRequiredService<IDInfrastructure>();
            i.ApplicationStartup();
        }
    }
}
