using FluentAssertions;
using Pwneu.Api.Constants;
using System.Reflection;

namespace Pwneu.UnitTests.Constants;

public class ConstantsTests
{
    [Theory]
    [MemberData(nameof(GetConstantClasses))]
    public void AllConstants_ShouldBeUnique(Type constantsClass)
    {
        var constants = constantsClass
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(f => f.IsLiteral && !f.IsInitOnly) // Only compile-time constants
            .Select(f => f.GetValue(null)?.ToString() ?? string.Empty)
            .ToList();

        constants.Should().OnlyHaveUniqueItems($"{constantsClass.Name} constants should be unique");
    }

    public static TheoryData<Type> GetConstantClasses() =>
        [
            typeof(CommonConstants),
            typeof(ConfigurationKeys),
            typeof(RateLimitingPolicies),
            typeof(Roles),
            typeof(BuilderConfigurations),
            typeof(AuthorizationPolicies),
        ];
}
