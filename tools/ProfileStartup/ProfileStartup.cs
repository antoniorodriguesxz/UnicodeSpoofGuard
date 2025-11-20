using System;
using System.Diagnostics;
using UnicodeSpoofGuard.Detection;

Console.WriteLine("UnicodeSpoofGuard startup profiler");
Console.WriteLine("---------------------------------");

Measure("Cold detector instantiation", () => new SpoofDetector());
Measure("Warm detector instantiation", () =>
{
    for (int i = 0; i < 10; i++)
    {
        _ = new SpoofDetector();
    }
});
Measure("Single analysis (canonicalize + detect)", () =>
{
    var detector = new SpoofDetector();
    detector.Analyze("paypal-security-center.com");
});

static void Measure(string label, Action action)
{
    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();

    var stopwatch = Stopwatch.StartNew();
    action();
    stopwatch.Stop();

    Console.WriteLine($"{label.PadRight(40)} {stopwatch.Elapsed.TotalMilliseconds,8:F2} ms");
}

