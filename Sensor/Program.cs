using System.Net.Sockets;

Console.WriteLine("Sensor");

var serverIp = "127.0.0.1";
var serverPort = 19000;
var udpClient = new UdpClient();
var random = new Random();
var idBytes = new byte[4];
random.NextBytes(idBytes);

uint sensorId = (uint)idBytes[0] | ((uint)idBytes[1] << 8) | ((uint)idBytes[2] << 16) | ((uint)idBytes[3] << 24);
Console.WriteLine($"Sensor ID: {sensorId}");

ushort packetNumber = 0;
var temperature = 2200;
var humidity = 4500;
var pressure = 3200;
var voltage = 2200;
while (true)
{
    try
    {
        var temperatureChange = random.Next(-10, 11);
        temperature = temperature + temperatureChange;
        if (temperature < 1800)
        {
            temperature = 1800;
        }
        if (temperature > 2600)
        {
            temperature = 2600;
        }
        var humidityChange = random.Next(-30, 31);
        humidity = humidity + humidityChange;
        if (humidity < 3000)
        {
            humidity = 3000;
        }
        if (humidity > 7000)
        {
            humidity = 7000;
        }
        var pressureChange = random.Next(-20, 21);
        pressure = pressure + pressureChange;
        if (pressure < 2000)
        {
            pressure = 2000;
        }
        if (pressure > 4500)
        {
            pressure = 4500;
        }
        var voltageChange = random.Next(-10, 11);
        voltage = voltage + voltageChange;
        if (voltage < 2070)
        {
            voltage = 2070;
        }
        if (voltage > 2530)
        {
            voltage = 2530;
        }
        var data = new byte[14];
        data[0] = (byte)sensorId;
        data[1] = (byte)(sensorId >> 8);
        data[2] = (byte)(sensorId >> 16);
        data[3] = (byte)(sensorId >> 24);
        data[4] = (byte)packetNumber;
        data[5] = (byte)(packetNumber >> 8);
        var temperatureValue = (ushort)(short)temperature;
        data[6] = (byte)temperatureValue;
        data[7] = (byte)(temperatureValue >> 8);
        data[8] = (byte)humidity;
        data[9] = (byte)(humidity >> 8);
        data[10] = (byte)pressure;
        data[11] = (byte)(pressure >> 8);
        data[12] = (byte)voltage;
        data[13] = (byte)(voltage >> 8);

        await udpClient.SendAsync(data, data.Length, serverIp, serverPort);
        Console.WriteLine($"ID: {sensorId} | Packet: {packetNumber} | " + $"Temp: {temperature / 100.0:F2} C | " + $"Humidity: {humidity / 100.0:F2}% | " + $"Pressure: {pressure / 1000.0:F3} bar | " + $"Voltage: {voltage / 10.0:F1} V");
        packetNumber++;
        await Task.Delay(100);
        if (Console.KeyAvailable)
        {
            var key = Console.ReadKey(true);
            if (key.Key == ConsoleKey.Escape)
            {
                break;
            }
        }
    }
    catch (SocketException ex)
    {
        Console.WriteLine($"Network error: {ex.Message}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: {ex.Message}");
        await Task.Delay(1000);
    }
}
udpClient.Close();

Console.WriteLine("Sensor stopped.");
