using System.Text;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Identity.Shared.Data;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;

namespace Pwneu.Identity.Features.Users;

public static class ExportMembers
{
    public record Query : IRequest<Result<string>>;

    internal sealed class Handler(ApplicationDbContext context)
        : IRequestHandler<Query, Result<string>>
    {
        public async Task<Result<string>> Handle(Query request, CancellationToken cancellationToken)
        {
            var members = await context.Users
                .Where(user => !context.UserRoles
                    .Where(ur => ur.RoleId == context.Roles
                        .Where(role => role.Name == Consts.Manager)
                        .Select(role => role.Id)
                        .FirstOrDefault())
                    .Select(ur => ur.UserId)
                    .Contains(user.Id))
                .Select(user => new UserFullNameResponse
                {
                    Id = user.Id,
                    FullName = user.FullName
                })
                .ToListAsync(cancellationToken);

            var membersCsv = new StringBuilder();
            membersCsv.AppendLine("Id,Fullname");

            foreach (var member in members)
            {
                membersCsv.AppendLine($"{member.Id},{member.FullName}");
            }

            return membersCsv.ToString();
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("members/export", async (ISender sender) =>
                {
                    var query = new Query();
                    var result = await sender.Send(query);

                    return result.IsFailure
                        ? Results.StatusCode(500)
                        : Results.File(
                            Encoding.UTF8.GetBytes(result.Value),
                            contentType: "text/csv",
                            fileDownloadName: "pwneu-members.csv");
                })
                .RequireAuthorization(Consts.ManagerAdminOnly)
                .RequireRateLimiting(Consts.Generate)
                .WithTags(nameof(Users));
        }
    }
}