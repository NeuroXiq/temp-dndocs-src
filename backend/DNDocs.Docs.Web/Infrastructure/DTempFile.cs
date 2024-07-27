using System.Runtime.CompilerServices;

namespace DNDocs.Docs.Web.Infrastructure
{
    public class DTempFile : IDisposable
    {
        public string FilePath { get; set; }

        bool isDisposed = false;

        private DTempFile(string path)
        {
            FilePath = path;
        }

        public void Dispose()
        {
            if (isDisposed) return;
            isDisposed = true;

            try
            {
                if (File.Exists(FilePath)) File.Delete(FilePath);
            }
            catch (Exception e)
            {
            }
        }

        public static DTempFile Create()
        {
            var filePath = Path.GetTempFileName();

            return new DTempFile(filePath);
        }
    }
}
