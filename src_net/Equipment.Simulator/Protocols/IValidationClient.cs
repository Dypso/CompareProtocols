using Common.Models;

namespace Equipment.Simulator.Protocols;

public interface IValidationClient : IAsyncDisposable
{
    Task ConnectAsync();
    Task SendValidationAsync(ValidationEvent validation);
}