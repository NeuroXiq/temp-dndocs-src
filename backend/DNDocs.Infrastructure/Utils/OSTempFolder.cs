using DNDocs.Domain.Utils;

namespace DNDocs.Infrastructure.Utils
{
    internal class OSTempFolder : IDisposable, IOSTempFolder
    {
        private bool disposed;

        string osPath;
        public string OSFullPath
        {
            get
            {
                if (disposed) throw new InvalidOperationException("Cannot use tempfolder because is disposed (deleted)");
                return osPath;
            }
        }

        public OSTempFolder(string fullOsPath)
        {
            osPath = fullOsPath;
            disposed = false;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~OSTempFolder() => Dispose(false);

        protected virtual void Dispose(bool disposing)
        {
            try
            {
                if (disposed) return;

                if (disposing)
                {
                    // dispose managed objects
                }

                
                Directory.Delete(osPath, true);
            }
            catch
            {

            }
            finally
            {
                this.disposed = true;
            }
        }
    }
}
