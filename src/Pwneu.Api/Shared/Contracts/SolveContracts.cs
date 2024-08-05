namespace Pwneu.Api.Shared.Contracts;

public record UserSolveResponse(Guid ChallengeId, string ChallengeName, DateTime SolvedAt);

public record ChallengeSolveResponse(string UserId, string? UserName, DateTime SolvedAt);