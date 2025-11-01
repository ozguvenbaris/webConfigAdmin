using System.Text.Json;
using Config.Core.Models;
using StackExchange.Redis;

namespace Config.Core.Storage;

public sealed class RedisConfigStorage : IConfigStorage, IAsyncDisposable
{
    private readonly string _keyPrefix = "cfg:";
    public  ConnectionMultiplexer _mux;
    private readonly IDatabase _db;

    public RedisConfigStorage(string connectionString)
    {
        _mux = ConnectionMultiplexer.Connect(connectionString);
        _db = _mux.GetDatabase();
    }

    private string AppKey(string app) => $"{_keyPrefix}{app}"; // hash: field=name, value=json

    public async Task<IReadOnlyList<ConfigRecord>> GetAllAsync(string applicationName, CancellationToken ct = default)
    {
        var entries = await _db.HashGetAllAsync(AppKey(applicationName));
        var list = new List<ConfigRecord>(entries.Length);
        foreach (var e in entries)
        {
            var rec = JsonSerializer.Deserialize<ConfigRecord>(e.Value!)!;
            if (rec.IsActive)
            {
                list.Add(rec);
            }
        }
        return list;
    }

    public async Task UpsertAsync(ConfigRecord record, CancellationToken ct = default)
    {
        var appKey = AppKey(record.ApplicationName);
        var newField = record.Name.Trim();

        var id = record.Id is { } guid && guid != Guid.Empty ? guid : Guid.NewGuid();

        var updatedRecord = record with { Id = id };

        var json = JsonSerializer.Serialize(updatedRecord);

        var allEntries = await _db.HashGetAllAsync(appKey);

        var existing = allEntries.FirstOrDefault(x =>
        {
            if (!x.Value.HasValue) return false;
            var existingRecord = JsonSerializer.Deserialize<ConfigRecord>(x.Value!);
            return existingRecord?.Id == id;
        });

        if (existing.Name.HasValue)
        {
            var oldField = existing.Name!;
            if (oldField != newField)
            {
                await _db.HashDeleteAsync(appKey, oldField);
            }
        }

        await _db.HashSetAsync(appKey, new HashEntry[]
        {
        new(newField, json)
        });

        await _db.StringSetAsync($"{appKey}:ver", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    }


    public async ValueTask DisposeAsync()
    {
        await Task.Yield();
        _mux.Dispose();
    }
}
