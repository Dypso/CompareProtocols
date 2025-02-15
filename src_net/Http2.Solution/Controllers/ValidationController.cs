using System.Text.Json;
using Common.Models;
using Http2.Solution.Services;
using Microsoft.AspNetCore.Mvc;

namespace Http2.Solution.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ValidationController : ControllerBase
{
    private readonly ILogger<ValidationController> _logger;
    private readonly ValidationApiService _validationService;

    public ValidationController(
        ILogger<ValidationController> logger,
        ValidationApiService validationService)
    {
        _logger = logger;
        _validationService = validationService;
    }

    [HttpPost("batch")]
    public async Task<IActionResult> ProcessBatch(
        [FromBody] List<ValidationEvent> validations,
        [FromHeader(Name = "X-Equipment-ID")] string equipmentId,
        CancellationToken cancellationToken)
    {
        if (validations.Count > 1000)
            return BadRequest("Batch too large");

        try
        {
            foreach (var validation in validations)
            {
                validation.EquipmentId = equipmentId;
                await _validationService.EnqueueValidation(validation);
            }

            return Ok(new { processed = validations.Count });
        }
        catch (OperationCanceledException)
        {
            return StatusCode(503, "Service temporarily unavailable");
        }
    }

    [HttpPost("stream")]
    public async Task StreamValidations(CancellationToken cancellationToken)
    {
        Response.StatusCode = 200;
        await Response.StartAsync(cancellationToken);

        var reader = new StreamReader(Request.Body);
        var writer = new StreamWriter(Response.Body);

        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrEmpty(line)) continue;

            try
            {
                var validation = JsonSerializer.Deserialize<ValidationEvent>(line);
                if (validation != null)
                {
                    await _validationService.EnqueueValidation(validation);
                    await writer.WriteLineAsync("ACK");
                    await writer.FlushAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing validation");
                await writer.WriteLineAsync("ERROR");
                await writer.FlushAsync();
            }
        }
    }
}