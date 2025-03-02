namespace Pwneu.Api.Common;

public record Error(string Code, string Message)
{
    public static readonly Error None = new(string.Empty, string.Empty);

    public static readonly Error NullValue = new(
        "Error.NullValue",
        "The specified result value is null."
    );

    public static readonly Error ConditionNotMet = new(
        "Error.ConditionNotMet",
        "The specified condition was not met."
    );

    public static readonly Error AnotherProcessRunning = new(
        "Error.AnotherProcessRunning",
        "Another system process is running. Please try again later."
    );
}
