using BoletoForBanrisul.Core.Converters;
using System.Text.Json;

namespace BoletoForBanrisul.Core.Tests.Converters
{
    /// <summary>
    /// <see cref="TruncateStringConverter"/> stays in the codebase as dead weight per the project's own
    /// decision (rejecting is now preferred to truncating), but it is still wired to several model
    /// properties alongside <c>[StringLength]</c>, so it still needs to behave correctly.
    /// </summary>
    [Trait("Category", "Unit")]
    public class TruncateStringConverterTests
    {
        [Theory]
        [InlineData("hello", 10, "hello")]
        [InlineData("hello world", 5, "hello")]
        [InlineData("", 5, "")]
        [InlineData("exact", 5, "exact")]
        public void Write_TruncatesToTheMaxLength(string input, int maxLength, string expected)
        {
            var options = new JsonSerializerOptions { Converters = { new TruncateStringConverter(maxLength) } };

            var json = JsonSerializer.Serialize(input, options);

            Assert.Equal($"\"{expected}\"", json);
        }

        [Fact]
        public void Write_Null_WritesJsonNull()
        {
            var options = new JsonSerializerOptions { Converters = { new TruncateStringConverter(5) } };

            var json = JsonSerializer.Serialize((string?)null, options);

            Assert.Equal("null", json);
        }

        [Fact]
        public void Read_DoesNotTruncate_ThePropertyOnlyTruncatesOnWrite()
        {
            var options = new JsonSerializerOptions { Converters = { new TruncateStringConverter(3) } };

            var value = JsonSerializer.Deserialize<string?>("\"hello world\"", options);

            Assert.Equal("hello world", value);
        }
    }
}
