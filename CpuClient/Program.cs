using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

const int port = 5000;
const int requestIntervalMilliseconds = 2000;
const int responseTimeoutMilliseconds = 1500;

using var client = new UdpClient();
using var shutdown = new CancellationTokenSource();
var serverAddress = new IPEndPoint(IPAddress.Loopback, port);
var request = Encoding.UTF8.GetBytes("cpu");
var keyboardTask = WatchForEscapeAsync(shutdown);

Console.WriteLine($"Sending CPU requests to {serverAddress} every 2 seconds.");
Console.WriteLine("Press Escape to stop.");

try
{
    while (!shutdown.IsCancellationRequested)
    {
        var cycleTimer = Stopwatch.StartNew();

        try
        {
            await client.SendAsync(request, serverAddress);
            using var responseTimeout = CancellationTokenSource.CreateLinkedTokenSource(shutdown.Token);
            responseTimeout.CancelAfter(responseTimeoutMilliseconds);

            var response = await client.ReceiveAsync(responseTimeout.Token);
            var cpuUsage = Encoding.UTF8.GetString(response.Buffer);
            Console.WriteLine($"{DateTime.Now:HH:mm:ss} | CPU usage: {cpuUsage}");
        }
        catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
        {
            break;
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine($"{DateTime.Now:HH:mm:ss} | No response from the server.");
        }
        catch (SocketException ex)
        {
            Console.WriteLine($"Network error: {ex.Message}");
        }
        catch (IOException ex)
        {
            Console.WriteLine($"Communication error: {ex.Message}");
        }

        var remainingMilliseconds = requestIntervalMilliseconds - (int)cycleTimer.ElapsedMilliseconds;
        if (remainingMilliseconds > 0)
        {
            try
            {
                await Task.Delay(remainingMilliseconds, shutdown.Token);
            }
            catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
finally
{
    shutdown.Cancel();
    await keyboardTask;
}

Console.WriteLine("CPU client stopped.");

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
