using FluentAssertions;
using Pwneu.Api.Constants;
using System.Reflection;

namespace Pwneu.UnitTests.Constants;

public class CacheKeysTests
{
    [Fact]
    public void AllCacheKeys_ShouldBeUnique()
    {
        var cacheKeyMethods = typeof(CacheKeys)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.ReturnType == typeof(string))
            .ToList();

        var generatedKeys = new List<string>();

        foreach (var method in cacheKeyMethods)
        {
            var parameters = method.GetParameters();
            string cacheKey;

            if (parameters.Length == 0)
            {
                // Parameterless methods
                cacheKey = (string)method.Invoke(null, null)!;
            }
            else
            {
                // Methods with parameters - generate test values
                var args = parameters.Select(p => GenerateTestValue(p.ParameterType)).ToArray();
                cacheKey = (string)method.Invoke(null, args)!;
            }

            generatedKeys.Add(cacheKey);
        }

        generatedKeys.Should().OnlyHaveUniqueItems("cache keys should be unique");
    }

    private static object GenerateTestValue(Type type)
    {
        return type == typeof(string) ? "test"
            : type == typeof(Guid) ? Guid.Empty
            : throw new NotSupportedException($"Unsupported parameter type: {type}");
    }
}
