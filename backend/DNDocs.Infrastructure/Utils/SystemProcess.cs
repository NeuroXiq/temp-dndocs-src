using Microsoft.Extensions.Logging;
using DNDocs.Domain.Utils;
using System.Diagnostics;

namespace DNDocs.Infrastructure.Utils
{
    internal class SystemProcess : ISystemProcess
    {
        private ILogger<SystemProcess> logger;

        public SystemProcess(ILogger<SystemProcess> logger)
        {
            this.logger = logger;
        }

        public void Start(
            string psiFilename,
            string psiArguments,
            int waitTime,
            out int exitCode,
            out string stdo,
            out string stderr,
            bool throwIfExitCodeNotZero = true,
            string workingDirectory = null)
        {
            stdo = "";
            stderr = "";

            logger.LogTrace("Starting process: {0} {1} \r\nWait time:{2}\r\nWorking Directory: {3}", psiFilename, psiArguments, waitTime, workingDirectory);

            using (Process p = new Process())
            {
                p.StartInfo.FileName = psiFilename;
                p.StartInfo.Arguments = psiArguments;
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.RedirectStandardError = true;
                p.StartInfo.WorkingDirectory = workingDirectory;
                p.Start();
                
                // p.PriorityClass = ProcessPriorityClass.RealTime; // not work on linux (permission denied)

                Thread.Sleep(5);
                int waiting = -1;

                do
                {
                    p.Refresh();
                    p.WaitForExit(1000);
                    stdo += p.StandardOutput.ReadToEnd();
                    stderr += p.StandardError.ReadToEnd();
                }
                while (!p.HasExited && (waiting++) < waitTime);

                var plog = string.Format("PSI_FILENAME: {0}\r\nPSI_ARGS: {1} \r\nPID: {2}\r\nSTDO: {3}\r\nSTDERR: {4}\r\n", psiFilename, psiArguments, p.Id, stdo, stderr);

                Validation.AppEx(!p.HasExited, $"Process did not exit after wait time.\r\n{plog}");

                exitCode = p.ExitCode;

                plog = $"EXIT_CODE: {exitCode}\r\n" + plog;

                logger.LogTrace($"Process exe completed: {plog}");

                if (throwIfExitCodeNotZero && exitCode != 0)
                {
                    throw new AppException($"Process exit code not zero. {psiFilename} {psiArguments ?? "<NULL>"} PID: {p.Id}");
                }
            }
        }
    }
}
