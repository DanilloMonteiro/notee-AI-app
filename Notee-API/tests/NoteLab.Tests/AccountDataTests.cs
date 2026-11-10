using NoteLab.Domain;
using Xunit;

namespace NoteLab.Tests;

public class AccountDataTests
{
    [Theory]
    [InlineData("Ana Souza", "Ana Souza")]
    [InlineData("  Ana   Souza  ", "Ana Souza")]
    [InlineData("Jo", "Jo")]
    public void NormalizeFullName_accepts_and_trims(string input, string expected) =>
        Assert.Equal(expected, AccountData.NormalizeFullName(input));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("A")]
    public void NormalizeFullName_rejects_empty_or_too_short(string? input) =>
        Assert.Null(AccountData.NormalizeFullName(input));

    [Fact]
    public void NormalizeFullName_rejects_too_long() =>
        Assert.Null(AccountData.NormalizeFullName(new string('a', ApplicationUser.FullNameMaxLength + 1)));

    [Theory]
    [InlineData("(11) 98765-4321", "11987654321")]
    [InlineData("11 3456-7890", "1134567890")]
    [InlineData("+55 11 98765-4321", "+5511987654321")]
    [InlineData("11.98765.4321", "11987654321")]
    public void NormalizePhone_keeps_only_digits(string input, string expected) =>
        Assert.Equal(expected, AccountData.NormalizePhone(input));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("123")]
    [InlineData("1234567890123456")]
    [InlineData("11 9876a-4321")]
    [InlineData("11+987654321")]
    public void NormalizePhone_rejects_empty_or_invalid(string? input) =>
        Assert.Null(AccountData.NormalizePhone(input));
}
