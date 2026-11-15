using System.Text.Json;

namespace ApplicationFoundry.Features.Jobs;

internal static class JobFeedReader
{
    internal const int MaxBytes = 16 * 1024 * 1024;

    internal static async Task<JsonDocument> ReadAsync(HttpContent content, CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength > MaxBytes)
            throw new InvalidDataException("Job feed exceeds the size limit.");

        await using var source = await content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await source.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > MaxBytes)
                throw new InvalidDataException("Job feed exceeds the size limit.");
            buffer.Write(chunk, 0, read);
        }
        buffer.Position = 0;
        return await JsonDocument.ParseAsync(buffer, cancellationToken: cancellationToken);
    }
}
