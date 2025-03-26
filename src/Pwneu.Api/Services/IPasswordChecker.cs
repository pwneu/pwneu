using Pwneu.Api.Common;
using Pwneu.Api.Constants;

namespace Pwneu.Api.Services;

/// <summary>
/// Defines the contract for password validation.
/// </summary>
public interface IPasswordChecker
{
    /// <summary>
    /// Validates the given password.
    /// </summary>
    /// <param name="password">The password to validate.</param>
    /// <returns>A <see cref="Result"/> indicating success or failure.</returns>
    Result IsPasswordAllowed(string password);
}

/// <inheritdoc />
public class ProductionPasswordChecker : IPasswordChecker
{
    /// <inheritdoc />
    public Result IsPasswordAllowed(string password)
    {
        if (password.Equals(CommonConstants.DevelopmentPassword))
            return Result.Failure(
                new Error(
                    "Password.NotAllowed",
                    $"The password '{CommonConstants.DevelopmentPassword}' is not allowed since it's a common password for developers and testers of PWNEU"
                )
            );

        return Result.Success();
    }
}

/// <inheritdoc />
public class DevelopmentPasswordChecker : IPasswordChecker
{
    /// <inheritdoc />
    public Result IsPasswordAllowed(string _) => Result.Success();
}
