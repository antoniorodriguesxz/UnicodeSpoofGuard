# Performance Notes

## Confusables Dataset Loading

The confusables dataset is now shipped as a compressed binary cache
(`data/confusables.bin`) that is memory-mapped on first use. The JSON source is
still available as a fallback, but normal execution avoids the larger parse and
allocation hit.

To validate the improvement, run the profiling harness:

```
dotnet run --configuration Release --project tools/ProfileStartup/ProfileStartup.csproj
```

Sample output on a Windows 11 workstation (AMD Ryzen 7 6800U):

```
UnicodeSpoofGuard startup profiler
---------------------------------
Cold detector instantiation               1143.90 ms
Warm detector instantiation                  0.34 ms
Single analysis (canonicalize + detect)     43.81 ms
```

> Tip: `python scripts/profile_startup.py` wraps the profiler and reports total
> execution time when Python is available on the host.

