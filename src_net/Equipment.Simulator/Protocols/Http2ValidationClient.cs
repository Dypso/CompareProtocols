using Common.Models;

namespace Equipment.Simulator.Protocols;

public class Http2ValidationClient : IValidationClient
{
    private readonly string _equipmentId;
    private readonly LocalCache _cache;
    private readonly HttpClient _client;
    private readonly List<ValidationEvent> _batch;
    private readonly SemaphoreSlim _batchLock = new(1);
    private readonly Timer _batchTimer;

    public Http2ValidationClient(string equipmentId, LocalCache cache)
    {
        _equipmentId = equipmentId;
        _cache = cache;
        _batch = new List<ValidationEvent>();
        
        _client = new HttpClient
        {
            BaseAddress = new Uri("https://localhost:5001"),
            DefaultRequestVersion = new Version(2, 0),
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact
        };
        
        _client.DefaultRequestHeaders.Add("X-Equipment-ID", equipmentId);
        
        _batchTimer = new Timer(
            async _ => await FlushBatchAsync(), 
            null, 
            TimeSpan.FromSeconds(1), 
            TimeSpan.FromSeconds(1));
    }

    public Task ConnectAsync() => Task.CompletedTask;

    public async Task SendValidationAsync(ValidationEvent validation)
    {
        await _batchLock.WaitAsync();
        try
        {
            _batch.Add(validation);
            if (_batch.Count >= 1000)
            {
                await FlushBatchAsync();
            }
        }
        finally
        {
            _batchLock.Release();
        }
    }

    private async Task FlushBatchAsync()
    {
        if (!_batch.Any()) return;

        await _batchLock.WaitAsync();
        try
        {
            var batchToSend = _batch.ToList();
            _batch.Clear();

            var response = await _client.PostAsJsonAsync(
                "api/validation/batch", 
                batchToSend);

            if (!response.IsSuccessStatusCode)
            {
                foreach (var validation in batchToSend)
                {
                    await _cache.StoreEvent(validation);
                }
            }
        }
        catch (Exception)
        {
            foreach (var validation in _batch)
            {
                await _cache.StoreEvent(validation);
            }
            throw;
        }
        finally
        {
            _batchLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await FlushBatchAsync();
        await _batchTimer.DisposeAsync();
        _client.Dispose();
    }
}