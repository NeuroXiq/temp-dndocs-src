using DbUp;
using DbUp.Engine;
using DbUp.Engine.Output;

namespace DNDocs.Infrastructure.Utils
{
    class log : IUpgradeLog
    {
        public void WriteError(string format, params object[] args)
        {
            Console.WriteLine(format, args);
        }

        public void WriteInformation(string format, params object[] args)
        {
            Console.WriteLine(format, args);
        }

        public void WriteWarning(string format, params object[] args)
        {
            Console.WriteLine(format, args);
        }
    }

    class TransactionProcessor : IScriptPreprocessor
    {
        public string Process(string contents)
        {
            if (contents.StartsWith("--DBUPINFO:NOTRANSACTION"))
            {
                return contents;
            }

            return "BEGIN TRANSACTION;\r\n" + contents + "\r\nCOMMIT;";
        }
    }

    public static class Migrations
    {
        public static void Run(string connectionString)
        {
            RawRobiniaInfrastructure.CreateSqliteDbIfNotExists(connectionString);

            var asm = typeof(RawRobiniaInfrastructure).Assembly;
            string location = typeof(RawRobiniaInfrastructure).Assembly.Location;

            var upgrader = DeployChanges.To
                .SQLiteDatabase(connectionString)
                .WithScriptsEmbeddedInAssembly(asm, (m) => m.StartsWith("DNDocs.Infrastructure.Migrations."))
                .LogScriptOutput()
                .LogTo(new log())
                .LogToConsole()
                //.WithTransaction() <-- needed by 0008 migration
                .Build();

            var result = upgrader.PerformUpgrade();

            if (!result.Successful)
            {
                throw result.Error;
            }
        }
    }
}
