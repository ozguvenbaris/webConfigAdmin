namespace Config.Core.Models;

public sealed record class ConfigRecord
{
    public Guid? Id { get; init; }
    public required string Name { get; init; }
    public required string Type { get; init; }
    public required string Value { get; init; }
    public required bool IsActive { get; init; }
    public required string ApplicationName { get; init; }
}
