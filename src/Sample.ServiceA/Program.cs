using Config.Core;
using Config.Core.Models;
using Config.Core.Storage;

var appName = "SERVICE-A";
var redisBootstrap = Environment.GetEnvironmentVariable("REDIS_CONNECTION") ?? "localhost:6379";

await using var cfg = new ConfigurationReader(appName, redisBootstrap, refreshTimerIntervalInMs: 2000);

//TEST
var record = new ConfigRecord { Id = Guid.NewGuid(), ApplicationName = "SERVICE-A", IsActive = true, Name = " 111", Type = "string", Value = "asd" };

var redisConnectionDef = new RedisConfigStorage(redisBootstrap);
await redisConnectionDef.UpsertAsync(record);


while (true)
{
    try
    {
        RedisConfigStorage? newRedisConnection = null;
        var newRedisConnectionString = cfg.GetValue<string>("Redis");

        if (redisBootstrap != newRedisConnectionString)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[CONFIG CHANGE] Redis connection updated:");
            Console.ResetColor();

            // Eski bağlantıyı kapat
            if (redisBootstrap is not null)
            {
                await redisConnectionDef._mux.CloseAsync();
                redisConnectionDef._mux.Dispose();
               
                Console.WriteLine("Old Redis connection closed.");
            }
            redisBootstrap = newRedisConnectionString;
            // Yeni bağlantıyı oluştur
            newRedisConnection = new RedisConfigStorage(newRedisConnectionString);
            Console.WriteLine("New Redis connection established.");

            
        }

        //Redis bağlantısı aktifse test yap
        if (newRedisConnection?._mux.IsConnected == true)
        {
            var db = newRedisConnection._mux.GetDatabase();
            var pong = await db.PingAsync();
            Console.WriteLine($"[{DateTime.Now:T}] Redis ping OK ({pong.TotalMilliseconds} ms)");
            var recordd = new ConfigRecord { Id = Guid.NewGuid(), ApplicationName = "SERVICE-C", IsActive = true, Name = "xxxxxxxxxxxxxxxx", Type = "string", Value = "ASASA" };
            await newRedisConnection.UpsertAsync(recordd);
        }
        else
        {
            Console.WriteLine("Redis connection inactive!");
        }
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"Error: {ex.Message}");
        Console.ResetColor();
    }

    await Task.Delay(3000);
}