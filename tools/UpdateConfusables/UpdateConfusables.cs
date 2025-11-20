using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

var options = UpdateOptions.Parse(args);

var reporter = new ConsoleReporter(options.Verbose);

try
{
    var updater = new ConfusablesUpdater(reporter);
    if (options.CheckOnly)
    {
        var isFresh = await updater.CheckFreshnessAsync(options);
        return isFresh ? 0 : 2;
    }

    await updater.UpdateAsync(options);
    return 0;
}
catch (Exception ex)
{
    reporter.Error(ex.Message);
    reporter.Debug(ex.ToString());
    return 1;
}

sealed record UpdateOptions(
    bool CheckOnly,
    bool SkipDownload,
    string OutputPath,
    string? LocalInput,
    bool Verbose,
    Uri BaseUri)
{
    public static UpdateOptions Parse(string[] args)
    {
        bool checkOnly = false;
        bool skipDownload = false;
        bool verbose = false;
        string? output = null;
        string? localInput = null;
        Uri baseUri = new("https://www.unicode.org/Public/security/latest/", UriKind.Absolute);

        var queue = new Queue<string>(args);
        while (queue.Count > 0)
        {
            var token = queue.Dequeue();
            switch (token)
            {
                case "--check":
                case "-c":
                    checkOnly = true;
                    break;
                case "--skip-download":
                    skipDownload = true;
                    break;
                case "--output":
                case "-o":
                    output = ExpectValue(queue, token);
                    break;
                case "--local":
                case "-l":
                    localInput = ExpectValue(queue, token);
                    break;
                case "--base":
                    baseUri = new Uri(ExpectValue(queue, token), UriKind.Absolute);
                    break;
                case "--verbose":
                case "-v":
                    verbose = true;
                    break;
                case "--help":
                case "-h":
                    PrintHelp();
                    Environment.Exit(0);
                    break;
                default:
                    throw new ArgumentException($"Unknown argument '{token}'. Use --help to see available options.");
            }
        }

        var repoRoot = RepoLocator.FindRepoRoot() ?? Directory.GetCurrentDirectory();
        string defaultOutput = Path.Combine(repoRoot, "data", "confusables.json");
        Directory.CreateDirectory(Path.GetDirectoryName(defaultOutput)!);

        return new UpdateOptions(
            checkOnly,
            skipDownload,
            output ?? defaultOutput,
            localInput,
            verbose,
            baseUri);
    }

    private static string ExpectValue(Queue<string> queue, string optionName) =>
        queue.Count > 0
            ? queue.Dequeue()
            : throw new ArgumentException($"Option {optionName} expects a value.");

    private static void PrintHelp()
    {
        Console.WriteLine(
@"Unicode Confusables Updater

Usage:
  dotnet run --project tools/UpdateConfusables -- [options]

Options:
  -c, --check            Check whether the bundled confusables dataset is current.
      --skip-download    Use existing downloaded artefacts (requires --local).
  -l, --local <path>     Use a local confusables.txt file instead of downloading.
  -o, --output <path>    Set the destination JSON path (defaults to data/confusables.json).
      --base <uri>       Override the Unicode Security data base URI.
  -v, --verbose          Emit verbose diagnostic output.
  -h, --help             Display this help message.
");
    }
}

sealed class ConfusablesUpdater(ConsoleReporter Reporter)
{
    private const string ConfusablesFileName = "confusables.txt";
    private static readonly JsonSerializerOptions jsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http = CreateHttpClient();

    public async Task UpdateAsync(UpdateOptions options)
    {
        Reporter.Info("Updating Unicode confusables dataset...");

        var remote = await FetchConfusablesAsync(options);
        var dataset = ConfusablesDataset.Create(remote);

        await WriteDatasetAsync(options.OutputPath, dataset);
        Reporter.Success($"Updated {options.OutputPath}");
    }

    public async Task<bool> CheckFreshnessAsync(UpdateOptions options)
    {
        Reporter.Info("Checking confusables dataset freshness...");

        if (!File.Exists(options.OutputPath))
        {
            Reporter.Error($"Dataset file '{options.OutputPath}' does not exist.");
            return false;
        }

        ConfusablesDataset? local;
        await using (var stream = File.OpenRead(options.OutputPath))
        {
            local = await JsonSerializer.DeserializeAsync<ConfusablesDataset>(stream, jsonOptions);
        }

        if (local is null)
        {
            Reporter.Error("Failed to parse the local dataset.");
            return false;
        }

        Reporter.Debug($"Local dataset version {local.SourceVersion} dated {local.SourceDate:u}.");

        var remote = await FetchConfusablesAsync(options with { SkipDownload = false });

        Reporter.Debug($"Remote dataset version {remote.Metadata.Version} dated {remote.Metadata.SourceDate:u}.");

        bool isFresh = local.SourceDate >= remote.Metadata.SourceDate &&
                       string.Equals(local.HashSha512, remote.Metadata.Sha512, StringComparison.OrdinalIgnoreCase);

        if (!isFresh)
        {
            Reporter.Warn("Bundled dataset is stale. Run without --check to refresh.");
        }
        else
        {
            Reporter.Success("Bundled dataset is current.");
        }

        return isFresh;
    }

    private async Task<RemoteConfusables> FetchConfusablesAsync(UpdateOptions options)
    {
        string textContent;
        ConfusablesMetadata meta;

        if (options.LocalInput is not null)
        {
            Reporter.Info($"Reading local file '{options.LocalInput}'.");
            textContent = await File.ReadAllTextAsync(options.LocalInput, Encoding.UTF8);
            meta = ConfusablesMetadata.FromContent(textContent, null, DateTimeOffset.UtcNow, null);
            return new RemoteConfusables(textContent, meta);
        }

        if (options.SkipDownload)
        {
            throw new InvalidOperationException("Cannot skip download without providing --local input.");
        }

        var confusablesUri = new Uri(options.BaseUri, ConfusablesFileName);
        Reporter.Info($"Downloading {confusablesUri}...");
        textContent = await _http.GetStringAsync(confusablesUri);

        string? sha512 = await TryFetchSha512Async(options.BaseUri);

        var fetchedAt = DateTimeOffset.UtcNow;
        meta = ConfusablesMetadata.FromContent(textContent, sha512, fetchedAt, confusablesUri);

        if (sha512 is not null)
        {
            Reporter.Info("Verifying SHA-512 digest...");
            using var sha = SHA512.Create();
            var digest = Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(textContent)));
            if (!digest.Equals(sha512, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("SHA-512 digest mismatch for confusables.txt.");
            }
            Reporter.Success("SHA-512 digest verified.");
        }
        else
        {
            Reporter.Warn("SHA-512 checksum unavailable; skipping verification.");
        }

        return new RemoteConfusables(textContent, meta);
    }

    private async Task<string?> TryFetchSha512Async(Uri baseUri)
    {
        try
        {
            var shaUri = new Uri(baseUri, $"{ConfusablesFileName}.sha512");
            Reporter.Debug($"Attempting to download checksum from {shaUri}...");
            var checksum = await _http.GetStringAsync(shaUri);
            return ParseSha512(checksum);
        }
        catch (HttpRequestException ex) when (ex.StatusCode is System.Net.HttpStatusCode.NotFound)
        {
            Reporter.Debug("No SHA-512 file available.");
            return null;
        }
    }

    private static string? ParseSha512(string checksumContent)
    {
        foreach (var token in checksumContent.Split(new[] { ' ', '\t', '\r', '\n', '*' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (token.Length == 128 && token.All(Uri.IsHexDigit))
            {
                return token.ToUpperInvariant();
            }
        }
        return null;
    }

    private static async Task WriteDatasetAsync(string outputPath, ConfusablesDataset dataset)
    {
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(outputPath);
        await JsonSerializer.SerializeAsync(stream, dataset, jsonOptions);
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("UnicodeSpoofGuard", "1.0"));
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("(confusables-updater)"));
        return client;
    }
}

sealed record RemoteConfusables(string RawContent, ConfusablesMetadata Metadata);

sealed record ConfusablesMetadata(
    string Version,
    DateTimeOffset SourceDate,
    string? Sha512,
    Uri? SourceUri,
    DateTimeOffset RetrievedAt)
{
    public static ConfusablesMetadata FromContent(string content, string? sha512, DateTimeOffset fetchedAt, Uri? sourceUri)
    {
        string version = "unknown";
        DateTimeOffset sourceDate = DateTimeOffset.MinValue;

        foreach (var line in ReadHeaderLines(content))
        {
            if (line.StartsWith("# Version:", StringComparison.OrdinalIgnoreCase))
            {
                version = line[(line.IndexOf(':') + 1)..].Trim();
            }
            else if (line.StartsWith("# Date:", StringComparison.OrdinalIgnoreCase))
            {
                var dateText = line[(line.IndexOf(':') + 1)..].Trim();
                if (DateTimeOffset.TryParse(dateText, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
                {
                    sourceDate = parsed.ToUniversalTime();
                }
            }
        }

        if (sourceDate == DateTimeOffset.MinValue)
        {
            sourceDate = fetchedAt;
        }

        return new ConfusablesMetadata(version, sourceDate, sha512, sourceUri, fetchedAt);
    }

    private static IEnumerable<string> ReadHeaderLines(string content)
    {
        using var reader = new StringReader(content);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (!line.StartsWith("#", StringComparison.Ordinal)) break;
            yield return line;
        }
    }
}

sealed record ConfusablesDataset(
    string SourceVersion,
    DateTimeOffset SourceDate,
    DateTimeOffset RetrievedAt,
    string? SourceUri,
    string? HashSha512,
    IReadOnlyList<ConfusableEntry> Entries)
{
    public static ConfusablesDataset Create(RemoteConfusables remote)
    {
        var entries = ConfusablesParser.Parse(remote.RawContent).ToArray();

        return new ConfusablesDataset(
            remote.Metadata.Version,
            remote.Metadata.SourceDate,
            remote.Metadata.RetrievedAt,
            remote.Metadata.SourceUri?.ToString(),
            remote.Metadata.Sha512,
            entries);
    }
}

sealed record ConfusableEntry(
    string Source,
    string Target,
    string? Type,
    string? Comment);

static class ConfusablesParser
{
    public static IEnumerable<ConfusableEntry> Parse(string content)
    {
        using var reader = new StringReader(content);
        string? line;
        int lineNumber = 0;
        while ((line = reader.ReadLine()) != null)
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;

            var commentSplit = line.Split('#', 2);
            var payload = commentSplit[0].Trim();
            var comment = commentSplit.Length > 1 ? commentSplit[1].Trim() : null;

            var parts = payload.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length < 2)
            {
                continue;
            }

            var source = HexToString(parts[0]);
            var target = HexToString(parts[1]);
            var type = parts.Length > 2 ? parts[2].Trim() : null;

            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(target))
            {
                continue;
            }

            yield return new ConfusableEntry(source, target, type, comment);
        }
    }

    private static string HexToString(string hexSequence)
    {
        var builder = new StringBuilder();
        foreach (var token in hexSequence.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (int.TryParse(token, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int codePoint))
            {
                builder.Append(char.ConvertFromUtf32(codePoint));
            }
        }
        return builder.ToString();
    }
}

sealed class ConsoleReporter(bool verbose)
{
    private readonly ConcurrentQueue<(ConsoleColor? Color, string Message)> _buffer = new();

    public bool Verbose { get; } = verbose;

    public void Info(string message) => Write(ConsoleColor.Cyan, message);

    public void Success(string message) => Write(ConsoleColor.Green, message);

    public void Warn(string message) => Write(ConsoleColor.Yellow, message);

    public void Error(string message) => Write(ConsoleColor.Red, message);

    public void Debug(string message)
    {
        if (Verbose)
        {
            Write(ConsoleColor.DarkGray, message);
        }
    }

    private void Write(ConsoleColor color, string message) => Write((ConsoleColor?)color, message);

    private void Write(ConsoleColor? color, string message)
    {
        lock (_buffer)
        {
            var previous = Console.ForegroundColor;
            if (color.HasValue)
            {
                Console.ForegroundColor = color.Value;
            }
            Console.WriteLine(message);
            if (color.HasValue)
            {
                Console.ForegroundColor = previous;
            }
        }
    }
}

static class RepoLocator
{
    public static string? FindRepoRoot()
    {
        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current is not null)
        {
            if (current.GetFiles("UnicodeSpoofGuard.sln").Length > 0 ||
                current.GetDirectories(".git").Length > 0)
            {
                return current.FullName;
            }
            current = current.Parent;
        }
        return null;
    }
}

