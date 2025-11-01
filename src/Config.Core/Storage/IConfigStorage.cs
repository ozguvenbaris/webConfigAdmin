using Config.Core.Models;

namespace Config.Core.Storage;

public interface IConfigStorage
{
    Task<IReadOnlyList<ConfigRecord>> GetAllAsync(string applicationName, CancellationToken ct = default);
    Task UpsertAsync(ConfigRecord record, CancellationToken ct = default);
}
