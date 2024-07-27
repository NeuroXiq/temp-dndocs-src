namespace DNDocs.Domain.Utils
{
    public interface IOSTempFolder : IDisposable
    {
        string OSFullPath { get; }
    }
}
