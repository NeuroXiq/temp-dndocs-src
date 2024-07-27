using Dapper;
using DNDocs.Docs.Web.Infrastructure;
using DNDocs.Docs.Web.Model;
using DNDocs.Docs.Web.Shared;
using Microsoft.Data.Sqlite;
using Microsoft.Net.Http.Headers;
using SQLitePCL;
using System.Data.SQLite;
using System.Drawing;
using System.Runtime.CompilerServices;
using Vinca.Http.Logs;

namespace DNDocs.Docs.Web.Service
{
    public interface ITxRepository : IDisposable
    {
        void BeginTransaction();
        Task CommitAsync();
        Task RollbackAsync();

        Task InsertSharedSiteItem(SharedSiteItem newShared);
        Task InsertSitemap(Sitemap sitemap);
        Task<IEnumerable<Sitemap>> SelectSitemap(bool includeData = false);

        Task InsertSiteHtmlAsync(List<SiteItem> items);
        Task<Project> SelectProjectByIdAsync(long id);
        Task UpdateProjectAsync(Project project);
        Task InsertProjectAsync(Project project);

        Task InsertHttpLogAsync(IList<VHttpLog> logs);
        Task<Project> SelectVersionProject(string urlPrefix, string version);
        Task<Project> SelectSingletonProjectAsync(string urlPrefix);
        Task DeleteSiteHtmlByProjectIdAsync(long projectId);
        Task InsertAppLogAsync(IEnumerable<AppLog> logRows);
        
        Task<IEnumerable<string>> SelectSiteItemPathByProjectId(long projectId);
        Task DeleteProjectAsync(long projectId);
        Task<long?> SelectSharedSiteItemIdBySha256(string sha256);
        Task InsertResourceMonitorUtilization(ResourceMonitorUtilization rmu);

        // other
        Task<IEnumerable<long>> ScriptForSitemapGenerator();
    }

    /// <summary>
    /// everyting same as DRepository but using transactions, must register as scoped
    /// </summary>
    public class TxRepository : ITxRepository
    {
        SQLiteConnection appConnection = null;
        SQLiteConnection siteConnection = null;
        SQLiteConnection logConnection = null;
        SQLiteConnection varSiteConnection = null;
        SQLiteTransaction appTx = null;
        SQLiteTransaction siteTx = null;
        SQLiteTransaction logTx = null;
        SQLiteTransaction varSiteTx = null;

        bool isDisposed = false;
        bool isBeginTx = false;
        bool commitOrRollback = false;
        IDInfrastructure infrastructure;

        public TxRepository(IDInfrastructure infrastructure)
        {
            this.infrastructure = infrastructure;
        }

        #region other

        public async Task<IEnumerable<long>> ScriptForSitemapGenerator()
        {
            var conn = GetSqliteConnection(DatabaseType.VarSite);
            var sql = $"{infrastructure.AttachDatabase(DatabaseType.App)} AS [appdb]; ";
            await conn.ExecuteAsync(sql);

            sql =
                "DELETE FROM project_sitemap AS sp WHERE NOT EXISTS (SELECT p.id FROM [appdb].project p WHERE p.id = sp.project_id);"+
                "DELETE FROM sitemap AS s WHERE NOT EXISTS (SELECT sp.id FROM project_sitemap sp WHERE sp.sitemap_id = s.id);" + 
                "DELETE FROM sitemap WHERE sitemap_name = 'sitemapindex.xml'";

            await conn.ExecuteAsync(sql);

            sql = $"SELECT id FROM [appdb].project p " +
                "WHERE NOT EXISTS (SELECT sp.id FROM project_sitemap sp WHERE sp.project_id = p.id);";

            IEnumerable<long> needSitemaps = await conn.QueryAsync<long>(sql);

            return needSitemaps;
        }

        public async Task InsertResourceMonitorUtilization(ResourceMonitorUtilization rmu)
        {
            var conn = GetSqliteConnection(DatabaseType.Log);
            var sql = $"INSERT INTO resource_monitor_utilization (" +
                "cpu_used_percentage, memory_used_in_bytes, " +
                "memory_used_percentage, date_time )" +
                "VALUES (@CpuUsedPercentage, @MemoryUsedInBytes, @MemoryUsedPercentage, @DateTime)";
            await conn.ExecuteAsync(sql, rmu);
        }
        
        #endregion

        #region logs

        public async Task InsertAppLogAsync(IEnumerable<AppLog> logs)
        {
            var connection = GetSqliteConnection(DatabaseType.Log);
            await connection.ExecuteAsync(
                "INSERT INTO app_log([message], category_name, log_level_id, event_id, event_name, [date]) VALUES (" +
                $"@{nameof(AppLog.Message)}," +
                $"@{nameof(AppLog.CategoryName)}," +
                $"@{nameof(AppLog.LogLevelId)}," +
                $"@{nameof(AppLog.EventId)}," +
                $"@{nameof(AppLog.EventName)}," +
                $"@{nameof(AppLog.Date)}" +
                ")",
                logs);
        }

        #endregion

        #region SiteItem

        public async Task InsertSiteHtmlAsync(List<SiteItem> items)
        {
            var connection = GetSqliteConnection(DatabaseType.Site);
            var sql = "INSERT INTO site_item(project_id, [path], shared_site_item_id, byte_data) " + 
                "VALUES (@ProjectId, @Path, @SharedSiteItemId, @ByteData)";
            await connection.ExecuteAsync(sql, items);
        }

        public async Task<IEnumerable<string>> SelectSiteItemPathByProjectId(long projectId)
        {
            var connection = GetSqliteConnection(DatabaseType.Site);
            var sql = $"SELECT [path] FROM site_item WHERE project_id = @ProjectId";

            return await connection.QueryAsync<string>(sql, new { ProjectId = projectId });
        }

        public async Task DeleteSiteHtmlByProjectIdAsync(long projectId)
        {
            var connection = GetSqliteConnection(DatabaseType.Site);
            var sql = "DELETE FROM site_item WHERE project_id = @ProjectId";
            await connection.ExecuteAsync(sql, new { ProjectId = projectId });
        }

        #endregion


        #region SharedSiteItemDb

        public async Task<IEnumerable<Sitemap>> SelectSitemap(bool includeByteData = false)
        {
            var connection = GetSqliteConnection(DatabaseType.VarSite);
            var sql = "SELECT id as Id, sitemap_name as SitemapName, created_on as CreatedOn, " + 
                "updated_on as UpdatedOn, ";

            if (includeByteData) sql += "byte_data as ByteData ";
            else sql += "NULL as ByteData ";

            sql += "FROM sitemap";

            return await connection.QueryAsync<Sitemap>(sql);
        }

        public async Task InsertSitemap(Sitemap sitemap)
        {
            var connection = GetSqliteConnection(DatabaseType.VarSite);
            var sql =
                $"INSERT INTO sitemap(sitemap_name, created_on, updated_on, byte_data) VALUES " +
                "(@SitemapName, @CreatedOn, @UpdatedOn, @ByteData)";
            await connection.ExecuteAsync(sql, sitemap);
        }

        public async Task InsertSharedSiteItem(SharedSiteItem newShared)
        {
            var connection = GetSqliteConnection(DatabaseType.VarSite);
            var sql = "INSERT INTO shared_site_item(path, byte_data, sha_256) " +
                "VALUES (@Path, @ByteData, @Sha256)";
            await connection.ExecuteAsync(sql, newShared);
            
            newShared.Id = connection.LastInsertRowId;
        }

        public async Task<long?> SelectSharedSiteItemIdBySha256(string sha256)
        {
            var connection = GetSqliteConnection(DatabaseType.VarSite);
            var sql = $"{SqlText.SelectSharedSiteItem} WHERE sha_256 = @Sha256";
            return await connection.QuerySingleOrDefaultAsync<long?>(sql, new { Sha256 = sha256 });
        }

        #endregion

        #region Project
        public async Task DeleteProjectAsync(long id)
        {
            var connection = GetSqliteConnection(DatabaseType.App);
            var sql = $"DELETE FROM project WHERE id = @Id";
            await connection.ExecuteAsync(sql, new { Id = id });
        }

        public async Task<Project> SelectVersionProject(string urlPrefix, string version)
        {
            var connection = GetSqliteConnection(DatabaseType.App);
            var sql = $"{SqlText.SelectProject} WHERE url_prefix = @UrlPrefix AND project_version = @ProjectVersion";
            var result = await connection.QueryFirstOrDefaultAsync<Project>(sql, new { UrlPRefix = urlPrefix, ProjectVersion = version });
            
            return result;
        }

        public async Task<Project> SelectSingletonProjectAsync(string urlPrefix)
        {
            var connection = GetSqliteConnection(DatabaseType.App);
            var sql = $"{SqlText.SelectProject} WHERE url_prefix = @UrlPrefix AND project_type = @ProjectType";
            return await connection.QuerySingleOrDefaultAsync<Project>(sql, new { UrlPrefix = urlPrefix, ProjectType = ProjectType.Singleton });
        }

        public async Task<Project> SelectNugetProjectAsync(string nugetPackageName, string nugetPackageVersion)
        {
            var connection = GetSqliteConnection(DatabaseType.App);
            var sql = $"{SqlText.SelectProject} WHERE nuget_package_name = @NugetPackageName AND nuget_package_version = @NugetPackageVersion AND project_type = @ProjectType";

            return await connection.QuerySingleOrDefaultAsync<Project>(
                sql,
                new { NugetPackageName = nugetPackageName, NugetPackageVersion = nugetPackageVersion, ProjectType = ProjectType.Nuget });
        }

        public async Task<Project> SelectProjectByIdAsync(long id)
        {
            var connection = GetSqliteConnection(DatabaseType.App);
            var sql = $"{SqlText.SelectProject} WHERE id = {id}";

            return await connection.QuerySingleOrDefaultAsync<Project>(sql);
        }

        public async Task UpdateProjectAsync(Project project)
        {
            var connection = GetSqliteConnection(DatabaseType.App);
            var sql = $"UPDATE project SET " +
                "dn_project_id = @DnProjectId, metadata = @Metadata, " +
                "url_prefix = @UrlPrefix, project_version = @ProjectVersion, " +
                "nuget_package_name = @NugetPackageName, nuget_package_version = @NugetPackageVersion, " +
                "project_type = @ProjectType, created_on = @CreatedOn, updated_on = @UpdatedOn " +
                $"WHERE id = {project.Id}";

            var affectedRows = await connection.ExecuteAsync(sql, project);
        }

        public async Task InsertProjectAsync(Project project)
        {
            var connection = GetSqliteConnection(DatabaseType.App);
            var sql = $"INSERT INTO project" +
                "(dn_project_id, metadata, url_prefix, project_version, nuget_package_name, " +
                "nuget_package_version, project_type, created_on, updated_on) " +
                "VALUES (@DnProjectId, @Metadata, @UrlPrefix, @ProjectVersion, @NugetPackageName, " +
                "@NugetPackageVersion, @ProjectType, @CreatedOn, @UpdatedOn)";

            var affected = await connection.ExecuteAsync(sql, project);
            project.Id = connection.LastInsertRowId;
        }

        #endregion

        public async Task InsertHttpLogAsync(IList<VHttpLog> logs)
        {
            var connection = GetSqliteConnection(DatabaseType.Log);
            var sql = $"INSERT INTO http_log " +
                $"([date], [time], client_ip, client_port, method, uri_path, uri_query, response_status, bytes_send, " +
                "bytes_received, time_taken_ms, host, user_agent, referer) VALUES " +
                "(@Date, @Time, @ClientIP, @ClientPort, @Method, @UriPath, @UriQuery, @ResponseStatus, @BytesSend, " +
                "@BytesReceived, @TimeTakenMs, @Host, @UserAgent, @Referer)";

            await connection.ExecuteAsync(sql, logs);
        }

        public void Dispose()
        {
            if (isDisposed) return;
            isDisposed = true;
            

            if (isBeginTx && !commitOrRollback)
            {
                var transactions = new SQLiteTransaction[] { appTx, siteTx, logTx };
                foreach (var tx in transactions)
                {
                    try { tx?.Rollback(); } catch { }
                }
            }

            appConnection?.Dispose();
            siteConnection?.Dispose();
            logConnection?.Dispose();
            varSiteConnection?.Dispose();

            appTx?.Dispose();
            siteTx?.Dispose();
            logTx?.Dispose();
            varSiteTx?.Dispose();
        }

        private SQLiteConnection GetSqliteConnection(DatabaseType databaseType)
        {
            GetCurrentTxConnection(databaseType, out var connection, out var _);

            if (connection != null) return connection;

            connection = infrastructure.CreateSqliteConnection(databaseType);
            connection.Open();

            switch (databaseType)
            {
                case DatabaseType.App:
                    appConnection = connection;
                    appTx = connection.BeginTransaction();
                    break;
                case DatabaseType.Site:
                    siteConnection = connection;
                    siteTx = connection.BeginTransaction();
                    break;
                case DatabaseType.Log:
                    logConnection = connection;
                    logTx = connection.BeginTransaction();
                    break;
                case DatabaseType.VarSite:
                    varSiteConnection = connection;
                    varSiteTx = connection.BeginTransaction();
                    break;
                default: throw new ArgumentException();
            }

            return connection;
        }

        private void GetCurrentTxConnection(DatabaseType type, out SQLiteConnection connection, out SQLiteTransaction transaction)
        {
            switch (type)
            {
                case DatabaseType.App: connection = appConnection; transaction = appTx; break;
                case DatabaseType.Site: connection = siteConnection; transaction = siteTx; break;
                case DatabaseType.Log: connection = logConnection; transaction = logTx; break;
                case DatabaseType.VarSite: connection = varSiteConnection; transaction = varSiteTx; break;
                default: throw new ArgumentException();
            }
        }

        public void BeginTransaction()
        {
            DValidation.ThrowISE(isBeginTx, "already begin tx, need to commit/rollback before open new");
            
            isBeginTx = true;
            commitOrRollback = false;
        }

        public async Task RollbackAsync()
        {
            DValidation.ThrowISE(!isBeginTx, "not transaction begin");
            DValidation.ThrowISE(commitOrRollback, "already commited or rolledback");

            if (appTx != null) await appTx.RollbackAsync();
            if(siteTx != null) await siteTx.RollbackAsync();
            if(logTx != null) await logTx.RollbackAsync();
            if(varSiteTx != null) await varSiteTx.RollbackAsync();

            commitOrRollback = true;
        }

        public async Task CommitAsync()
        {
            DValidation.ThrowISE(!isBeginTx, "not transaction begin");
            DValidation.ThrowISE(commitOrRollback, "already commited or rolledback");

            if (appTx != null) await appTx.CommitAsync();
            if(siteTx != null) await siteTx.CommitAsync();
            if(logTx != null) await logTx.CommitAsync();
            if(varSiteTx != null) await varSiteTx.CommitAsync();


            commitOrRollback = true;
        }
    }
}
