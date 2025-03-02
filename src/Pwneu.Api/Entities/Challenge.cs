using System.ComponentModel.DataAnnotations;

namespace Pwneu.Api.Entities;

public sealed class Challenge
{
    public Guid Id { get; init; }
    public Guid CategoryId { get; init; }
    public DateTime CreatedAt { get; init; }

    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;
    public int Points { get; set; }
    public bool DeadlineEnabled { get; set; }
    public DateTime Deadline { get; set; }
    public int MaxAttempts { get; set; }
    public int SolveCount { get; set; }
    public List<string> Tags { get; set; } = [];
    public List<string> Flags { get; set; } = [];
    public Category Category { get; init; } = null!;
    public ICollection<Hint> Hints { get; init; } = [];
    public ICollection<Artifact> Artifacts { get; init; } = [];
    public ICollection<Submission> Submissions { get; init; } = [];
    public ICollection<Solve> Solves { get; init; } = [];

    private Challenge() { }

    public static Challenge Create(
        Guid categoryId,
        string name,
        string description,
        int points,
        bool deadlineEnabled,
        DateTime deadline,
        int maxAttempts,
        List<string> tags,
        List<string> flags
    )
    {
        return new Challenge
        {
            Id = Guid.CreateVersion7(),
            CategoryId = categoryId,
            CreatedAt = DateTime.UtcNow,
            Name = name,
            Description = description,
            Points = points,
            DeadlineEnabled = deadlineEnabled,
            Deadline = deadline.ToUniversalTime(),
            MaxAttempts = maxAttempts,
            Tags = tags,
            Flags = flags,
            Hints = [],
            Artifacts = [],
        };
    }
}
