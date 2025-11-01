using System.Text.Json;
using Config.Core.Models;
using Config.Core.Storage;

var builder = WebApplication.CreateBuilder(args);

var redisCnn = Environment.GetEnvironmentVariable("REDIS_CONNECTION") ?? "localhost:6379";
builder.Services.AddSingleton<IConfigStorage>(_ => new RedisConfigStorage(redisCnn));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/api/config", async (string appName, IConfigStorage storage, CancellationToken ct) =>
{
    var list = await storage.GetAllAsync(appName, ct);
    return Results.Ok(list.OrderBy(r => r.Name));
});

app.MapPost("/api/config", async (ConfigRecord rec, IConfigStorage storage, CancellationToken ct) =>
{
    await storage.UpsertAsync(rec with { Name = rec.Name.Trim() }, ct);
    return Results.Ok();
});

app.MapPut("/api/config/{name}", async (string name, string appName, ConfigRecord body, IConfigStorage storage, CancellationToken ct) =>
{
    var rec = body with { Name = name.Trim(), ApplicationName = appName };
    await storage.UpsertAsync(rec, ct);
    return Results.Ok();
});

app.Run("http://0.0.0.0:80");

