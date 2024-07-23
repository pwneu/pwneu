namespace Pwneu.Api.Shared.Contracts;

public record SolveResponse(Guid Id, string ChallengeName, int Points, DateTime SolvedAt);