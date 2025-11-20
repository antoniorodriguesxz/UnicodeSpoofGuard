# Operations Guide

## Threat Feed Synchronisation

UnicodeSpoofGuard ships with a pluggable threat-feed pipeline that aggregates
blocklists into the embedded `data/threat_indicators.json` dataset. Use the CLI
to refresh the bundle whenever external feeds are updated:

```
dotnet run --project src/Cli/UnicodeSpoofGuard.Cli -- sync-feeds --verbose
```

By default the command resolves relative feed URIs against the repository root
and rewrites `data/threat_indicators.json`. Pass `--dry-run` to inspect the
resulting JSON without modifying disk, or `--output <path>` to export to an
alternate location.

### Scheduling

- **Windows Task Scheduler**
  - Create a new basic task executing `dotnet` with the arguments
    `run --project C:\repos\UnicodeSpoofGuard\src\Cli\UnicodeSpoofGuard.Cli -- sync-feeds`.
  - Configure a trigger that matches your threat-intelligence refresh cadence
    (e.g., every 6 hours).
  - Mark *Start in* as the repository root so relative feed paths resolve.

- **Cron (Linux/macOS)**
  - Add a cron entry such as:

    ```
    0 */6 * * * cd /opt/UnicodeSpoofGuard && dotnet run --project src/Cli/UnicodeSpoofGuard.Cli -- sync-feeds
    ```

The CLI exits with a non-zero code if any feed fails to download or parse,
making it safe to plug into CI or monitoring workflows.

