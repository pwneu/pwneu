namespace Pwneu.Api.Contracts;

public record AddHintRequest
{
    public string Content { get; set; } = default!;
    public int Deduction { get; set; }
}

public record HintResponse
{
    public Guid Id { get; init; }
    public int Deduction { get; set; }
}

public record HintDetailsResponse
{
    public Guid Id { get; init; }
    public string Content { get; set; } = default!;
    public int Deduction { get; set; }
}
