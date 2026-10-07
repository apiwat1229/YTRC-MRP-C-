using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Text;
using System.Text.RegularExpressions;
using YTRC.ERP.Application.Configuration;
using YTRC.ERP.Application.Contracts;
using YTRC.ERP.Domain.Models;

namespace YTRC.ERP.Infrastructure.Devices;

public sealed class HardwareScaleService : IHardwareScaleService
{
    private readonly ScaleConnection[] _connections = [new(), new()];
    public event EventHandler<ScaleChangedEventArgs>? ReadingChanged;

    public bool IsConnected(int index) => _connections[index].IsConnected;
    public ScaleReading? LatestReading(int index) => _connections[index].Latest;

    public async Task ConnectAsync(int index, ScaleSettings config)
    {
        Validate(index);
        await DisconnectAsync(index);
        var state = _connections[index];
        state.Config = config;
        state.Buffer.Clear();
        if (config.Simulation)
        {
            state.IsConnected = true;
            state.SimulationStop = new CancellationTokenSource();
            state.SimulationTask = SimulateAsync(index, state.SimulationStop.Token);
            return;
        }

        var serial = new SerialPort(config.PortName, config.BaudRate, (Parity)Math.Clamp(config.Parity, 0, 2),
            config.DataBits, config.StopBits == 2 ? StopBits.Two : StopBits.One)
        {
            ReadTimeout = 500,
            WriteTimeout = 500,
            NewLine = "\n"
        };
        serial.DataReceived += (_, _) => OnSerialData(index);
        serial.Open();
        state.Serial = serial;
        state.IsConnected = true;
    }

    public async Task DisconnectAsync(int index)
    {
        Validate(index);
        var state = _connections[index];
        state.IsConnected = false;
        state.SimulationStop?.Cancel();
        if (state.SimulationTask is not null)
        {
            try { await state.SimulationTask; } catch (OperationCanceledException) { }
        }
        state.SimulationStop?.Dispose();
        state.SimulationStop = null;
        state.SimulationTask = null;
        if (state.Serial is not null)
        {
            try { state.Serial.Close(); } catch { }
            state.Serial.Dispose();
            state.Serial = null;
        }
        state.Latest = null;
        ReadingChanged?.Invoke(this, new(index, null, false));
    }

    public IReadOnlyList<string> EnumeratePorts() => SerialPort.GetPortNames()
        .OrderBy(p => int.TryParse(p.AsSpan(3), out var n) ? n : int.MaxValue).ToArray();

    public static bool TryParse(string raw, string model, int decimals, out ScaleReading? reading)
    {
        reading = null;
        double weight;
        int places;
        bool stable = true;
        var tiger = Regex.Match(raw, "\\x02([+-])([ 0-9]{6})(\\d)([0-9A-Fa-f]{2})\\x03");
        if ((model.Contains("Tiger TI", StringComparison.OrdinalIgnoreCase) || model.Contains("Tiger TWT", StringComparison.OrdinalIgnoreCase)) && tiger.Success)
        {
            var digits = tiger.Groups[2].Value.Replace(" ", "");
            if (!double.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out weight) ||
                !int.TryParse(tiger.Groups[3].Value, out places)) return false;
            weight /= Math.Pow(10, places);
            if (tiger.Groups[1].Value == "-") weight = -weight;
        }
        else if (model.Contains("Toledo", StringComparison.OrdinalIgnoreCase))
        {
            var toledo = Regex.Match(raw, "(ST|US),GS,\\s*([+-]?)\\s*(\\d+(?:[.,]\\d+)?)\\s*(kg|lb)?", RegexOptions.IgnoreCase);
            if (!toledo.Success) return false;
            var number = toledo.Groups[3].Value.Replace(',', '.');
            if (!double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out weight)) return false;
            if (toledo.Groups[2].Value == "-") weight = -weight;
            places = number.Contains('.') ? number.Length - number.IndexOf('.') - 1 : 0;
            stable = toledo.Groups[1].Value.Equals("ST", StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            var generic = Regex.Match(raw, "([+-]?\\d+(?:[.,]\\d+)?)");
            if (!generic.Success) return false;
            var number = generic.Groups[1].Value.Replace(',', '.');
            if (!double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out weight)) return false;
            places = number.Contains('.') ? number.Length - number.IndexOf('.') - 1 : decimals;
        }
        reading = new ScaleReading(weight, places, stable, "kg", raw.Trim());
        return true;
    }

    private async Task SimulateAsync(int index, CancellationToken token)
    {
        var config = _connections[index].Config;
        var weight = index == 0 ? 25.40 : 1.250;
        var step = 0;
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(250));
        while (await timer.WaitForNextTickAsync(token).ConfigureAwait(false))
        {
            step++;
            var stable = step % 15 != 0;
            var value = stable ? weight : weight + (Random.Shared.NextDouble() - .5) * .04;
            var text = value.ToString($"F{config.DecimalPlaces}", CultureInfo.InvariantCulture) + " kg (SIM)";
            Publish(index, new(value, config.DecimalPlaces, stable, "kg", text));
        }
    }

    private void OnSerialData(int index)
    {
        var state = _connections[index];
        try
        {
            var incoming = state.Serial?.ReadExisting() ?? "";
            lock (state.Buffer)
            {
                state.Buffer.Append(incoming);
                while (true)
                {
                    var at = -1;
                    for (var i = 0; i < state.Buffer.Length; i++)
                        if (state.Buffer[i] is '\r' or '\n' or '\x03') { at = i; break; }
                    if (at < 0) break;
                    var line = state.Buffer.ToString(0, at + 1);
                    state.Buffer.Remove(0, at + 1);
                    if (TryParse(line, state.Config.Model, state.Config.DecimalPlaces, out var reading) && reading is not null)
                        Publish(index, reading);
                }
                if (state.Buffer.Length > 1024) state.Buffer.Clear();
            }
        }
        catch (InvalidOperationException) { }
        catch (IOException) { }
    }

    private void Publish(int index, ScaleReading reading)
    {
        _connections[index].Latest = reading;
        ReadingChanged?.Invoke(this, new(index, reading, true));
    }

    private static void Validate(int index)
    {
        if (index is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(index));
    }

    public void Dispose()
    {
        foreach (var state in _connections)
        {
            state.IsConnected = false;
            state.SimulationStop?.Cancel();
            state.Serial?.Dispose();
            state.Serial = null;
        }
    }

    private sealed class ScaleConnection
    {
        public ScaleSettings Config { get; set; } = new();
        public bool IsConnected { get; set; }
        public ScaleReading? Latest { get; set; }
        public SerialPort? Serial { get; set; }
        public CancellationTokenSource? SimulationStop { get; set; }
        public Task? SimulationTask { get; set; }
        public StringBuilder Buffer { get; } = new();
    }
}
