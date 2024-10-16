using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Identity.Shared.Data;
using Pwneu.Identity.Shared.Services;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;

namespace Pwneu.Identity.Features.Users;

public class GetMemberDetails
{
    public record Query(string Id) : IRequest<Result<UserDetailsResponse>>;

    private static readonly Error NotFound = new("GetMemberDetails.NotFound", "Member not found");

    internal sealed class Handler(ApplicationDbContext context, IAccessControl accessControl)
        : IRequestHandler<Query, Result<UserDetailsResponse>>
    {
        public async Task<Result<UserDetailsResponse>> Handle(Query request, CancellationToken cancellationToken)
        {
            var managerIds = await accessControl.GetManagerIdsAsync(cancellationToken);

            if (managerIds.Contains(request.Id))
                return Result.Failure<UserDetailsResponse>(NotFound);

            var user = await context
                .Users
                .Where(u => u.Id == request.Id)
                .Select(u => new UserDetailsResponse
                {
                    Id = u.Id,
                    UserName = u.UserName,
                    FullName = u.FullName,
                    CreatedAt = u.CreatedAt,
                    Email = u.Email
                })
                .FirstOrDefaultAsync(cancellationToken);

            return user ?? Result.Failure<UserDetailsResponse>(NotFound);
        }
    }
}

public class GetMemberDetailsConsumer(ISender sender, ILogger<GetMemberDetailsConsumer> logger)
    : IConsumer<GetMemberDetailsRequest>
{
    public async Task Consume(ConsumeContext<GetMemberDetailsRequest> context)
    {
        try
        {
            var message = context.Message;
            var query = new GetMemberDetails.Query(message.Id);
            var result = await sender.Send(query);

            if (result.IsSuccess)
            {
                logger.LogInformation("Successfully get user details: {id}", result.Value);
                await context.RespondAsync(result.Value);
                return;
            }

            logger.LogError("Failed to get user details: {message}", result.Error.Message);
            await context.RespondAsync(new UserDetailsNotFoundResponse());
        }
        catch (Exception e)
        {
            logger.LogError("{e}", e.Message);
        }
    }
}