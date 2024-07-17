namespace Pwneu.Api.Shared.Entities;

public class Challenge
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Points { get; set; }
    public bool DeadlineEnabled { get; set; }
    public DateTime Deadline { get; set; }
    public int MaxAttempts { get; set; }
    public List<string> Flags { get; set; } = [];
    public ICollection<ChallengeFile> ChallengeFiles { get; set; } = [];
    public ICollection<FlagSubmission> FlagSubmissions { get; set; } = [];
}