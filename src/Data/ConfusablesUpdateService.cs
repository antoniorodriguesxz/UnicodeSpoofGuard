using System;
using System.Net.Http;
using System.Text;
using UnicodeSpoofGuard.Detection;
using UnicodeSpoofGuard.Structures;

namespace UnicodeSpoofGuard.Data;

internal static class ConfusablesUpdateService
{
    internal const string DefaultConfusablesUrl = "https://www.unicode.org/Public/security/latest/confusables.txt";

    private static readonly HttpClient HttpClient = CreateHttpClient();

    public static RefreshConfusablesResult Refresh(string? confusablesUrl, byte[]? confusablesContent)
    {
        try
        {
            var resolvedUrl = string.IsNullOrWhiteSpace(confusablesUrl)
                ? DefaultConfusablesUrl
                : confusablesUrl!;

            var content = ResolveContent(resolvedUrl, confusablesContent);

            var dataset = ConfusablesDatasetFactory.FromText(content, resolvedUrl);

            ConfusablesDatasetLoader.SetDataset(dataset);
            ConfusablesIndex.Reload(dataset);

            return new RefreshConfusablesResult
            {
                IsSuccess = true,
                Message = "Confusables dataset refreshed successfully.",
                SourceVersion = dataset.SourceVersion,
                SourceDate = dataset.SourceDate.DateTime,
                RetrievedAt = dataset.RetrievedAt.DateTime,
                EntryCount = dataset.Entries.Count
            };
        }
        catch (Exception ex)
        {
            return new RefreshConfusablesResult
            {
                IsSuccess = false,
                Message = ex.Message
            };
        }
    }

    private static string ResolveContent(string url, byte[]? confusablesContent)
    {
        if (confusablesContent is { Length: > 0 })
        {
            try
            {
                return Encoding.UTF8.GetString(confusablesContent);
            }
            catch (DecoderFallbackException ex)
            {
                throw new InvalidOperationException("Confusables binary content is not valid UTF-8 text.", ex);
            }
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException("ConfusablesUrl must be an absolute URI.");
        }

        try
        {
            return HttpClient.GetStringAsync(uri).GetAwaiter().GetResult();
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException($"Failed to download confusables data from '{url}'.", ex);
        }
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("UnicodeSpoofGuard/1.0 (confusables-refresh)");
        return client;
    }
}

