using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Api.Common;
using Pwneu.Api.Constants;
using Pwneu.Api.Data;
using System.Text;

namespace Pwneu.Api.Features.Users;

public static class ExportMembers
{
    public record Query : IRequest<Result<string>>;

    internal sealed class Handler(AppDbContext context) : IRequestHandler<Query, Result<string>>
    {
        public async Task<Result<string>> Handle(Query request, CancellationToken cancellationToken)
        {
            var members = await context
                .Users.Where(user =>
                    !context
                        .UserRoles.Where(ur =>
                            ur.RoleId
                            == context
                                .Roles.Where(role => role.Name == Roles.Manager)
                                .Select(role => role.Id)
                                .FirstOrDefault()
                        )
                        .Select(ur => ur.UserId)
                        .Contains(user.Id)
                )
                .Select(user => new
                {
                    user.Id,
                    user.FullName,
                    user.UserName,
                    user.Email,
                    user.Points,
                })
                .ToListAsync(cancellationToken);

            var membersCsv = new StringBuilder();
            membersCsv.AppendLine("Id,Fullname,Username,Email,Points");

            foreach (var member in members)
            {
                membersCsv.AppendLine(
                    $"{member.Id},{member.FullName},{member.UserName},{member.Email},{member.Points}"
                );
            }

            return membersCsv.ToString();
        }
    }

    public class Endpoint : IV1Endpoint
    {
        public void MapV1Endpoint(IEndpointRouteBuilder app)
        {
            app.MapGet(
                    "identity/members/export",
                    async (ISender sender) =>
                    {
                        var query = new Query();
                        var result = await sender.Send(query);

                        return result.IsFailure
                            ? Results.StatusCode(500)
                            : Results.File(
                                Encoding.UTF8.GetBytes(result.Value),
                                contentType: "text/csv",
                                fileDownloadName: "pwneu-members.csv"
                            );
                    }
                )
                .RequireAuthorization(AuthorizationPolicies.ManagerAdminOnly)
                .RequireRateLimiting(RateLimitingPolicies.FileGeneration)
                .WithTags(nameof(Users));
        }
    }
}
