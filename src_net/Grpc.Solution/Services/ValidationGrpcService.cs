using Grpc.Core;
using Common.Models;
using System.Threading.Channels;

namespace Grpc.Solution.Services;

public class ValidationGrpcService : ValidationService.ValidationServiceBase
{
    private readonly ILogger<ValidationGrpcService> _logger;
    private readonly ValidationProcessor _processor;
    private static readonly SemaphoreSlim _throttle = new(1000);

    public ValidationGrpcService(
        ILogger<ValidationGrpcService> logger,
        ValidationProcessor processor)
    {
        _logger = logger;
        _processor = processor;
    }

    public override async Task<ValidationResponse> SendValidation(
        ValidationRequest request, 
        ServerCallContext context)
    {
        try
        {
            using var _ = await _throttle.WaitAsync(TimeSpan.FromSeconds(5))
                .ConfigureAwait(false);

            var validation = MapToValidationEvent(request);
            await _processor.ProcessValidation(validation);

            return new ValidationResponse { Success = true };
        }
        catch (OperationCanceledException)
        {
            throw new RpcException(new Status(
                StatusCode.ResourceExhausted, 
                "Service is currently overloaded"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing validation");
            throw new RpcException(new Status(
                StatusCode.Internal, 
                "Internal error occurred"));
        }
    }

    public override async Task StreamValidations(
        IAsyncStreamReader<ValidationRequest> requestStream,
        IServerStreamWriter<ValidationResponse> responseStream,
        ServerCallContext context)
    {
        var equipmentId = context.RequestHeaders.GetValue("equipment-id");
        if (string.IsNullOrEmpty(equipmentId))
        {
            throw new RpcException(new Status(
                StatusCode.InvalidArgument, 
                "Equipment ID is required"));
        }

        try
        {
            await foreach (var request in requestStream.ReadAllAsync())
            {
                var validation = MapToValidationEvent(request);
                validation.EquipmentId = equipmentId;

                await _processor.ProcessValidation(validation);
                await responseStream.WriteAsync(new ValidationResponse 
                { 
                    Success = true 
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in validation stream");
            throw new RpcException(new Status(
                StatusCode.Internal, 
                "Stream processing error"));
        }
    }

    public override async Task<BatchResponse> SendBatch(
        BatchRequest request, 
        ServerCallContext context)
    {
        if (request.Validations.Count > 1000)
        {
            throw new RpcException(new Status(
                StatusCode.InvalidArgument, 
                "Batch size exceeds maximum limit of 1000"));
        }

        var response = new BatchResponse();
        foreach (var validation in request.Validations)
        {
            try
            {
                await _processor.ProcessValidation(
                    MapToValidationEvent(validation));
                response.ProcessedCount++;
            }
            catch (Exception)
            {
                response.FailedIds.Add(validation.EquipmentId);
            }
        }

        return response;
    }

    private static ValidationEvent MapToValidationEvent(ValidationRequest request)
    {
        return new ValidationEvent
        {
            EquipmentId = request.EquipmentId,
            CardId = request.CardId,
            Timestamp = DateTime.FromFileTimeUtc(request.Timestamp),
            Location = request.Location,
            Amount = (decimal)request.Amount,
            Result = (Common.Models.ValidationResult)request.Result,
            Sequence = request.Sequence,
            SessionId = request.SessionId
        };
    }
}