using AP.Shared.Utilities.Security;
using FluentAssertions;
using Xunit;

namespace AP.Shared.Tests.Utilities;

public class PasswordPolicyTests
{
    [Theory]
    [InlineData("", false)]
    [InlineData("short1", false)]
    [InlineData("onlyletters", false)]
    [InlineData("12345678", false)]
    [InlineData("Admin123", true)]
    [InlineData("admin123", true)]
    public void Validate_EnforcesLengthLetterAndDigit(string password, bool expected)
    {
        PasswordPolicy.Validate(password).Succeeded.Should().Be(expected);
    }

    [Fact]
    public void Generate_ProducesReadablePasswordWithRequiredCategories()
    {
        var password = PasswordGenerator.Generate(12);

        password.Should().HaveLength(12);
        password.Any(char.IsLetter).Should().BeTrue();
        password.Any(char.IsDigit).Should().BeTrue();
        password.Any(c => "IO01".Contains(c)).Should().BeFalse();
    }
}
