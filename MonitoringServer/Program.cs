using System.Net.Sockets;

const int port = 19000;
using var receiver = new UdpClient(port);
using var shutdown = new CancellationTokenSource();

var readings = new Dictionary<uint, SensorReading>();
var readingsLock = new object();
var acceptedPackets = 0;
var latePackets = 0;
var invalidPackets = 0;

Console.WriteLine($"Monitoring server is listening on UDP port {port}.");
Console.WriteLine("Press Escape to stop.");

var receiveTask = ReceiveReadingsAsync();
var displayTask = RefreshTableAsync();
var keyboardTask = WatchForEscapeAsync();
await Task.WhenAll(receiveTask, displayTask, keyboardTask);

Console.WriteLine("Monitoring server stopped.");

async Task ReceiveReadingsAsync()
{
    while (!shutdown.IsCancellationRequested)
    {
        try
        {
            var datagram = await receiver.ReceiveAsync(shutdown.Token);
            if (!TryDecodeReading(datagram.Buffer, out var incomingReading))
            {
                lock (readingsLock)
                {
                    invalidPackets++;
                }

                continue;
            }

            lock (readingsLock)
            {
                if (readings.TryGetValue(incomingReading.SensorId, out var previousReading)
                    && IsLateOrDuplicate(incomingReading.PacketNumber, previousReading.PacketNumber))
                {
                    latePackets++;
                    continue;
                }

                readings[incomingReading.SensorId] = incomingReading;
                acceptedPackets++;
            }
        }
        catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
        {
            return;
        }
        catch (SocketException ex)
        {
            if (!shutdown.IsCancellationRequested)
            {
                Console.WriteLine($"Network error: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            if (!shutdown.IsCancellationRequested)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
    }
}

async Task RefreshTableAsync()
{
    var previousRowCount = 0;

    while (!shutdown.IsCancellationRequested)
    {
        try
        {
            lock (readingsLock)
            {
                var currentTime = DateTime.Now;
                var inactiveSensors = new List<uint>();

                foreach (var reading in readings)
                {
                    if ((currentTime - reading.Value.LastReceived).TotalSeconds > 5)
                    {
                        inactiveSensors.Add(reading.Key);
                    }
                }

                foreach (var sensorId in inactiveSensors)
                {
                    readings.Remove(sensorId);
                }

                Console.SetCursorPosition(0, 0);
                Console.WriteLine("BUILDING SENSOR MONITOR".PadRight(100));
                Console.WriteLine($"{"ID",-13}{"Temperature",-16}{"Humidity",-13}{"Pressure",-13}{"Voltage",-13}{"Last packet",-15}");

                var currentRowCount = 0;
                foreach (var item in readings)
                {
                    var reading = item.Value;
                    var elapsedMilliseconds = (currentTime - reading.LastReceived).TotalMilliseconds;

                    Console.WriteLine(
                        $"{reading.SensorId,-13}" +
                        $"{reading.Temperature:F2} C".PadRight(16) +
                        $"{reading.Humidity:F2} %".PadRight(13) +
                        $"{reading.Pressure:F3} bar".PadRight(13) +
                        $"{reading.Voltage:F1} V".PadRight(13) +
                        $"{elapsedMilliseconds:F0} ms");

                    currentRowCount++;
                }

                for (var row = currentRowCount; row < previousRowCount; row++)
                {
                    Console.WriteLine(new string(' ', 100));
                }

                previousRowCount = currentRowCount;
                Console.WriteLine(new string('-', 100));
                Console.WriteLine($"Active sensors: {readings.Count}");
                Console.WriteLine($"Accepted packets: {acceptedPackets}");
                Console.WriteLine($"Late packets: {latePackets}");
                Console.WriteLine($"Invalid packets: {invalidPackets}");
                Console.WriteLine($"Time: {currentTime}");
            }

            await Task.Delay(250, shutdown.Token);
        }
        catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            if (!shutdown.IsCancellationRequested)
            {
                Console.WriteLine($"Display error: {ex.Message}");
                await Task.Delay(1000);
            }
        }
    }
}

async Task WatchForEscapeAsync()
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

static bool TryDecodeReading(byte[] data, out SensorReading reading)
{
    if (data.Length != 14)
    {
        reading = null!;
        return false;
    }

    var sensorId = (uint)data[0]
        | ((uint)data[1] << 8)
        | ((uint)data[2] << 16)
        | ((uint)data[3] << 24);
    var packetNumber = (ushort)(data[4] | (data[5] << 8));
    var temperatureBits = (ushort)(data[6] | (data[7] << 8));
    var temperatureCentiDegrees = unchecked((short)temperatureBits);
    var humidityHundredths = (ushort)(data[8] | (data[9] << 8));
    var pressureThousandths = (ushort)(data[10] | (data[11] << 8));
    var voltageTenths = (ushort)(data[12] | (data[13] << 8));

    reading = new SensorReading
    {
        SensorId = sensorId,
        PacketNumber = packetNumber,
        Temperature = temperatureCentiDegrees / 100.0,
        Humidity = humidityHundredths / 100.0,
        Pressure = pressureThousandths / 1000.0,
        Voltage = voltageTenths / 10.0,
        LastReceived = DateTime.Now
    };

    return true;
}

static bool IsLateOrDuplicate(ushort incomingNumber, ushort previousNumber)
{
    var forwardDistance = unchecked((ushort)(incomingNumber - previousNumber));
    return forwardDistance == 0 || forwardDistance >= 32768;
}

sealed class SensorReading
{
    public uint SensorId { get; init; }
    public ushort PacketNumber { get; init; }
    public double Temperature { get; init; }
    public double Humidity { get; init; }
    public double Pressure { get; init; }
    public double Voltage { get; init; }
    public DateTime LastReceived { get; init; }
}
