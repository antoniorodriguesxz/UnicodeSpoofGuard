using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using UnicodeSpoofGuard.Data;
using UnicodeSpoofGuard.Feeds;

namespace UnicodeSpoofGuard.Cli;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        if (args.Length == 0 || IsHelp(args[0]))
        {
            PrintUsage();
            return 0;
        }

        var command = args[0];
        var rest = args.Skip(1).ToArray();

        return command.ToLowerInvariant() switch
        {
            "sync-feeds" => await RunSyncFeedsAsync(ThreatFeedSyncOptions.Parse(rest)),
            _ => UnknownCommand(command)
        };
    }

    private static bool IsHelp(string value) =>
        value is "-h" or "--help" or "help";

    private static int UnknownCommand(string command)
    {
        Console.Error.WriteLine($"Unknown command '{command}'.");
        PrintUsage();
        return 1;
    }

    private static async Task<int> RunSyncFeedsAsync(ThreatFeedSyncOptions options)
    {
        var baseDir = options.BaseDirectory ?? RepoLocator.FindRepoRoot() ?? Directory.GetCurrentDirectory();
        var outputPath = options.OutputPath ?? Path.Combine(baseDir, "data", "threat_indicators.json");

        if (options.Verbose)
        {
            Console.WriteLine($"Using base directory: {baseDir}");
            Console.WriteLine($"Output path: {outputPath}");
        }

        var config = ThreatFeedConfigLoader.Config;
        using var manager = new ThreatFeedManager(baseDirectory: baseDir);
        var dataset = await manager.BuildDatasetAsync(config);

        var json = JsonSerializer.Serialize(dataset, new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });

        if (options.DryRun)
        {
            Console.WriteLine(json);
            return 0;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        await File.WriteAllTextAsync(outputPath, json);

        if (options.Verbose)
        {
            Console.WriteLine($"Threat indicator dataset refreshed at {outputPath}.");
        }

        return 0;
    }

    internal static void PrintUsage()
    {
        Console.WriteLine(
@"UnicodeSpoofGuard CLI

Usage:
  dotnet run --project src/Cli/UnicodeSpoofGuard.Cli -- [command] [options]

Commands:
  sync-feeds           Download configured threat intelligence feeds and refresh the dataset.

Options (sync-feeds):
  --output <path>      Destination path for the aggregated indicators (defaults to data/threat_indicators.json).
  --base <path>        Base directory used to resolve relative feed URIs.
  --dry-run            Do not write output; dump the resulting JSON to stdout.
  --verbose            Emit verbose progress information.
  -h, --help           Show this message.");
    }
}

internal sealed record ThreatFeedSyncOptions(
    bool DryRun,
    bool Verbose,
    string? OutputPath,
    string? BaseDirectory)
{
    public static ThreatFeedSyncOptions Parse(string[] args)
    {
        bool dryRun = false;
        bool verbose = false;
        string? output = null;
        string? baseDir = null;

        var queue = new Queue<string>(args);
        while (queue.Count > 0)
        {
            var token = queue.Dequeue();
            switch (token)
            {
                case "--dry-run":
                    dryRun = true;
                    break;
                case "--verbose":
                    verbose = true;
                    break;
                case "--output":
                    output = ExpectValue(queue, token);
                    break;
                case "--base":
                    baseDir = ExpectValue(queue, token);
                    break;
                case "-h":
                case "--help":
                    Program.PrintUsage();
                    Environment.Exit(0);
                    break;
                default:
                    throw new ArgumentException($"Unknown option '{token}'. Use --help for usage.");
            }
        }

        return new ThreatFeedSyncOptions(dryRun, verbose, output, baseDir);
    }

    private static string ExpectValue(Queue<string> queue, string option)
    {
        if (queue.Count == 0)
        {
            throw new ArgumentException($"Option '{option}' requires a value.");
        }

        return queue.Dequeue();
    }
}

internal static class RepoLocator
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