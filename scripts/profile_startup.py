#!/usr/bin/env python3
"""
Helper script to benchmark UnicodeSpoofGuard startup costs.

Runs the ProfileStartup harness (tools/ProfileStartup) and captures the
reported timings. Execute from the repository root:

    python scripts/profile_startup.py
"""

from __future__ import annotations

import argparse
import subprocess
import sys
import time


def run_profile(configuration: str) -> tuple[str, float]:
    command = [
        "dotnet",
        "run",
        "--configuration",
        configuration,
        "--project",
        "tools/ProfileStartup/ProfileStartup.csproj",
    ]

    start = time.perf_counter()
    result = subprocess.run(command, capture_output=True, text=True, check=True)
    elapsed = time.perf_counter() - start
    return result.stdout.strip(), elapsed


def main() -> int:
    parser = argparse.ArgumentParser(description="Profile UnicodeSpoofGuard startup time.")
    parser.add_argument(
        "--configuration",
        default="Release",
        choices=["Debug", "Release"],
        help="Build configuration passed to dotnet run (default: Release).",
    )
    args = parser.parse_args()

    try:
        output, elapsed = run_profile(args.configuration)
    except subprocess.CalledProcessError as exc:
        sys.stderr.write(exc.stderr or str(exc))
        return exc.returncode

    print(output)
    print("-" * 60)
    print(f"Total execution time: {elapsed * 1000:.2f} ms")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

