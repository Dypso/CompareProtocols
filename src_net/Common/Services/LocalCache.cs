using System.Collections.Concurrent;
using System.Text.Json;
using Common.Models;
using Microsoft.Extensions.Logging;

namespace Common.Services;

public class LocalCache
{
    private readonly string _basePath;
    private readonly ILogger<LocalCache> _logger;
    private readonly long _maxSizeBytes = 2L * 1024 * 1024 * 1024; // 2GB
    private readonly ConcurrentQueue<ValidationEvent> _memoryQueue = new();

    public LocalCache(string basePath, ILogger<LocalCache> logger)
    {
        _basePath = basePath;
        _logger = logger;
        Directory.CreateDirectory(basePath);
    }

    public async Task StoreEvent(ValidationEvent evt)
    {
        _memoryQueue.Enqueue(evt);
        
        if (_memoryQueue.Count >= 1000)
        {
            await FlushToDisk();
        }
    }

    private async Task FlushToDisk()
    {
        var batch = new List<ValidationEvent>();
        while (_memoryQueue.TryDequeue(out var evt))
        {
            batch.Add(evt);
        }
        
        var fileName = Path.Combine(_basePath, $"cache_{DateTime.UtcNow:yyyyMMddHHmmss}.json");
        await using var fs = File.Create(fileName);
        await JsonSerializer.SerializeAsync(fs, batch);
        
        _logger.LogInformation("Flushed {Count} events to {FileName}", batch.Count, fileName);
    }

    public async Task<IEnumerable<ValidationEvent>> RecoverEvents()
    {
        var files = Directory.GetFiles(_basePath, "*.json")
                           .OrderBy(f => f);
        
        var events = new List<ValidationEvent>();
        foreach (var file in files)
        {
            await using var fs = File.OpenRead(file);
            var batch = await JsonSerializer.DeserializeAsync<List<ValidationEvent>>(fs);
            if (batch != null)
            {
                events.AddRange(batch);
            }
        }
        
        return events;
    }
}