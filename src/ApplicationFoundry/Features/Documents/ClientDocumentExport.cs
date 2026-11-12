namespace ApplicationFoundry.Features.Documents;

public interface IClientDocumentExport
{
    bool IsAvailable { get; }
    Task<bool> SaveAsync(byte[] content, string fileName, string mediaType);
}

public sealed class BrowserDocumentExport : IClientDocumentExport
{
    public bool IsAvailable => false;
    public Task<bool> SaveAsync(byte[] content, string fileName, string mediaType) => Task.FromResult(false);
}
