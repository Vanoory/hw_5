using System.Diagnostics;
using System.Net.Sockets;
using System.Text;

const int port = 5000;
using var server = new UdpClient(port);
using var process = Process.GetCurrentProcess();
using var shutdown = new CancellationTokenSource();

var lastCpuTime = process.TotalProcessorTime;
var lastSampleTime = Stopwatch.GetTimestamp();
var keyboardTask = WatchForEscapeAsync(shutdown);

Console.WriteLine($"CPU server is listening on UDP port {port}.");
Console.WriteLine("Press Escape to stop.");

try
{
    while (!shutdown.IsCancellationRequested)
    {
        try
        {
            var request = await server.ReceiveAsync(shutdown.Token);
            var message = Encoding.UTF8.GetString(request.Buffer);
            var currentCpuTime = process.TotalProcessorTime;
            var currentSampleTime = Stopwatch.GetTimestamp();
            var elapsedMilliseconds = (currentSampleTime - lastSampleTime) * 1000.0 / Stopwatch.Frequency;
            var usedMilliseconds = (currentCpuTime - lastCpuTime).TotalMilliseconds;
            var cpuUsage = elapsedMilliseconds <= 0
                ? 0
                : Math.Clamp(usedMilliseconds / (elapsedMilliseconds * Environment.ProcessorCount) * 100, 0, 100);

            lastCpuTime = currentCpuTime;
            lastSampleTime = currentSampleTime;

            var answer = Encoding.UTF8.GetBytes($"{cpuUsage:F1}%");
            await server.SendAsync(answer, request.RemoteEndPoint);
            Console.WriteLine($"{DateTime.Now:HH:mm:ss} | {request.RemoteEndPoint.Address} | {message} | CPU: {cpuUsage:F1}%");
        }
        catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
        {
            break;
        }
        catch (SocketException ex)
        {
            if (!shutdown.IsCancellationRequested)
            {
                Console.WriteLine($"Network error: {ex.Message}");
            }
        }
    }
}
finally
{
    shutdown.Cancel();
    await keyboardTask;
}

Console.WriteLine("CPU server stopped.");

static async Task WatchForEscapeAsync(CancellationTokenSource shutdown)
{
    while (!shutdown.IsCancellationRequested)
    {
        try
        {
            if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape)
            {
                shutdown.Cancel();
                return;
            }
        }
        catch (InvalidOperationException)
        {
            await Task.Delay(250);
            continue;
        }
        catch (IOException)
        {
            await Task.Delay(250);
            continue;
        }

        await Task.Delay(100);
    }
}
