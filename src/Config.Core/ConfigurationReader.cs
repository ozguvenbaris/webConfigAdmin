using System.Collections.Concurrent;
using System.Globalization;
using Config.Core.Models;
using Config.Core.Storage;

namespace Config.Core;

public sealed class ConfigurationReader : IAsyncDisposable
{
    private readonly string _applicationName;
    private readonly IConfigStorage _storage;
    private readonly TimeSpan _interval;
    private readonly CancellationTokenSource _cts = new();
    private Task? _refreshLoop;
    public IReadOnlyDictionary<string, string> ActiveValues =>
    _active.ToDictionary(kv => kv.Key, kv => kv.Value.Value, StringComparer.OrdinalIgnoreCase);

    private ConcurrentDictionary<string, ConfigRecord> _active = new();
    private ConcurrentDictionary<string, ConfigRecord> _lastGood = new();

    public ConfigurationReader(string applicationName, string connectionString, int refreshTimerIntervalInMs)
    {
        _applicationName = applicationName ?? throw new ArgumentNullException(nameof(applicationName));
        _storage = new RedisConfigStorage(connectionString ?? throw new ArgumentNullException(nameof(connectionString)));
        _interval = TimeSpan.FromMilliseconds(Math.Max(250, refreshTimerIntervalInMs));
        _refreshLoop = StartAsync(_cts.Token);
    }

    private async Task StartAsync(CancellationToken ct)
    {
        await RefreshOnce(ct);

        var timer = new PeriodicTimer(_interval);
        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                await RefreshOnce(ct);
            }
        }
        catch (OperationCanceledException) { }
        finally { timer.Dispose(); }
    }

    private async Task RefreshOnce(CancellationToken ct)
    {
        try
        {
            var records = await _storage.GetAllAsync(_applicationName, ct);
            var dict = new ConcurrentDictionary<string, ConfigRecord>(
                records.Where(r => r.IsActive)
                       .ToDictionary(r => r.Name, r => r, StringComparer.OrdinalIgnoreCase));

            _active = dict;
            _lastGood = dict.Count > 0 ? dict : _lastGood;
        }
        catch
        {
        }
    }

    public T GetValue<T>(string key)
    {
        if (key is null) throw new ArgumentNullException(nameof(key));

        if (_active.TryGetValue(key, out var rec) && rec.IsActive)
            return ConvertValue<T>(rec.Type, rec.Value);

        if (_lastGood.TryGetValue(key, out rec) && rec.IsActive)
            return ConvertValue<T>(rec.Type, rec.Value);

        throw new KeyNotFoundException($"Key not found or inactive: {key}");
    }

    private static T ConvertValue<T>(string type, string value)
    {
        object boxed = type.ToLowerInvariant() switch
        {
            "string" => value,
            "int" or "integer" => int.Parse(value, CultureInfo.InvariantCulture),
            "double" => double.Parse(value, CultureInfo.InvariantCulture),
            "bool" or "boolean" => ParseBool(value),
            _ => throw new NotSupportedException($"Unsupported type: {type}")
        };
        return (T)Convert.ChangeType(boxed, typeof(T), CultureInfo.InvariantCulture);

        static bool ParseBool(string v)
        {
            if (bool.TryParse(v, out var b)) return b;
            if (v == "1") return true;
            if (v == "0") return false;
            throw new FormatException($"Invalid bool: {v}");
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        try { if (_refreshLoop is not null) await _refreshLoop; } catch { }
        if (_storage is IAsyncDisposable ad) await ad.DisposeAsync();
        _cts.Dispose();
    }
}
