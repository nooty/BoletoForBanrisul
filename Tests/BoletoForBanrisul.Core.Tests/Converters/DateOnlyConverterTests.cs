using BoletoForBanrisul.Core.Converters;
using System.Text.Json;

namespace BoletoForBanrisul.Core.Tests.Converters
{
    [Trait("Category", "Unit")]
    public class DateOnlyConverterTests
    {
        private static readonly JsonSerializerOptions Options = new() { Converters = { new DateOnlyConverter() } };

        [Fact]
        public void RoundTrips_ADate_AsYyyyMmDd()
        {
            var date = new DateTime(2026, 8, 30);

            var json = JsonSerializer.Serialize(date, Options);
            Assert.Equal("\"2026-08-30\"", json);

            var roundTripped = JsonSerializer.Deserialize<DateTime>(json, Options);
            Assert.Equal(date, roundTripped);
        }

        [Fact]
        public void Write_IgnoresAnyTimeOfDayComponent()
        {
            // The format is date-only ("yyyy-MM-dd"); a time component on the DateTime, if one somehow got
            // set, is silently dropped rather than rejected or rounded.
            var dateWithTime = new DateTime(2026, 8, 30, 13, 45, 0);

            var json = JsonSerializer.Serialize(dateWithTime, Options);

            Assert.Equal("\"2026-08-30\"", json);
        }

        [Fact]
        public void Read_AMalformedDate_Throws()
        {
            // DateOnlyConverter.Read calls DateTime.ParseExact with no try/catch, so a malformed value
            // surfaces as the framework's own FormatException rather than being translated to JsonException.
            Assert.Throws<FormatException>(() => JsonSerializer.Deserialize<DateTime>("\"not-a-date\"", Options));
        }

        [Fact]
        public void Read_ADateInTheWrongFormat_Throws()
        {
            // "30/08/2026" is a valid date, just not in the "yyyy-MM-dd" shape ParseExact requires.
            Assert.Throws<FormatException>(() => JsonSerializer.Deserialize<DateTime>("\"30/08/2026\"", Options));
        }
    }
}
