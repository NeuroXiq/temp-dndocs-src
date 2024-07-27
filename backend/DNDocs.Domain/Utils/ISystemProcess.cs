namespace DNDocs.Domain.Utils
{
    public interface ISystemProcess
    {
        void Start(
            string psiFilename,
            string psiArguments,
            int maxWaitTimeSeconds,
            out int exitCode,
            out string stdo,
            out string stderr,
            bool throwIfExitCodeNotZero = true,
            string psiWorkingDirectory = null);
    }
}
