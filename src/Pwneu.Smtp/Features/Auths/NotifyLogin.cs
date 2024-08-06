using MassTransit;
using Pwneu.Shared.Contracts;

namespace Pwneu.Smtp.Features.Auths;

public static class NotifyLogin
{
    public class Consumer : IConsumer<LoggedInEvent>
    {
        public async Task Consume(ConsumeContext<LoggedInEvent> context)
        {
            await Task.Delay(10000);
        }
    }
}