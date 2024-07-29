namespace Pwneu.Api.Shared.Contracts;

public record CreateCategoryRequest(string Name, string Description);

public record CategoryResponse(Guid Id, string Name, string Description, IEnumerable<ChallengeResponse> Challenges);

public record UpdateCategoryRequest(string Name, string Description);