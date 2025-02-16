using Grpc.Core;
using Common.Models;
using System.Threading.Channels;
using Google.Protobuf.WellKnownTypes;
using GrpcValidation;
using Microsoft.Extensions.Logging;

namespace Grpc.Solution.Services
{
    public class ValidationGrpcService : Validator.ValidatorBase
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

        public override async Task<ValidationResponse> ValidateSingle(
            ValidationRequest request, 
            ServerCallContext context)
        {
            try
            {
                if (!await _throttle.WaitAsync(TimeSpan.FromSeconds(5)))
                {
                    throw new RpcException(new Status(
                        StatusCode.ResourceExhausted, 
                        "Service is currently at capacity"));
                }

                try
                {
                    var validation = MapToValidationEvent(request);
                    await _processor.ProcessValidation(validation);
                    
                    return new ValidationResponse 
                    { 
                        Success = true,
                        Status = "VALIDATED",
                        ProcessedAt = Timestamp.FromDateTime(DateTime.UtcNow),
                        Message = "Validation processed successfully"
                    };
                }
                finally
                {
                    _throttle.Release();
                }
            }
            catch (OperationCanceledException)
            {
                throw new RpcException(new Status(
                    StatusCode.ResourceExhausted, 
                    "Request timed out due to high load"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing validation for equipment {EquipmentId}", request.EquipmentId);
                throw new RpcException(new Status(
                    StatusCode.Internal, 
                    $"Internal error: {ex.Message}"));
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
                    "Equipment ID header is required"));
            }

            try
            {
                await foreach (var request in requestStream.ReadAllAsync(context.CancellationToken))
                {
                    if (request.EquipmentId != equipmentId)
                    {
                        _logger.LogWarning("Equipment ID mismatch in stream. Header: {HeaderId}, Request: {RequestId}", 
                            equipmentId, request.EquipmentId);
                        continue;
                    }

                    try
                    {
                        var validation = MapToValidationEvent(request);
                        await _processor.ProcessValidation(validation);
                        
                        await responseStream.WriteAsync(new ValidationResponse 
                        { 
                            Success = true,
                            Status = "VALIDATED",
                            ProcessedAt = Timestamp.FromDateTime(DateTime.UtcNow),
                            Message = "Validation processed successfully"
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing stream validation for equipment {EquipmentId}", equipmentId);
                        await responseStream.WriteAsync(new ValidationResponse 
                        { 
                            Success = false,
                            Status = "ERROR",
                            ProcessedAt = Timestamp.FromDateTime(DateTime.UtcNow),
                            Message = $"Processing error: {ex.Message}"
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatal error in validation stream for equipment {EquipmentId}", equipmentId);
                throw new RpcException(new Status(
                    StatusCode.Internal, 
                    $"Stream processing error: {ex.Message}"));
            }
        }

        public override async Task<BatchResponse> ValidateBatch(
            BatchRequest request, 
            ServerCallContext context)
        {
            const int MaxBatchSize = 1000;
            if (request.Validations.Count > MaxBatchSize)
            {
                throw new RpcException(new Status(
                    StatusCode.InvalidArgument, 
                    $"Batch size exceeds maximum limit of {MaxBatchSize}"));
            }

            var response = new BatchResponse
            {
                ProcessedAt = Timestamp.FromDateTime(DateTime.UtcNow),
                Status = "PROCESSING"
            };

            foreach (var validation in request.Validations)
            {
                try
                {
                    await _processor.ProcessValidation(MapToValidationEvent(validation));
                    response.ProcessedCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing batch validation for token {TokenId}", validation.TokenId);
                    response.FailedValidationIds.Add(validation.TokenId);
                }
            }

            response.Status = response.FailedValidationIds.Count == 0 ? "COMPLETED" : "COMPLETED_WITH_ERRORS";
            response.Message = $"Processed {response.ProcessedCount} validations with {response.FailedValidationIds.Count} failures";

            return response;
        }

        private static ValidationEvent MapToValidationEvent(ValidationRequest request)
        {
            return new ValidationEvent
            {
                EquipmentId = request.EquipmentId,
                TokenId = request.TokenId,
                Timestamp = request.Timestamp.ToDateTime(),
                Location = request.Location,
                Amount = (decimal)request.Amount,
                Type = request.Type,
                Status = request.Status,
                Sequence = request.Sequence,
                SessionId = request.SessionId,
                Metadata = request.Metadata.ToDictionary(x => x.Key, x => x.Value)
            };
        }
    }
}