using BoletoForBanrisul.Core.Validation;

namespace BoletoForBanrisul.Core.Tests.Validation
{
    /// <summary>
    /// <see cref="DecimalScaleAttribute"/> exists because every decimal property on this model is serialized
    /// with a fixed number of decimal places ("F1"/"F2"/"F3"); any extra precision is silently rounded away on
    /// the wire, so a local comparison or sum run on the unrounded value can pass here and still be rejected -
    /// or silently misread - by the bank.
    /// </summary>
    [Trait("Category", "Unit")]
    public class DecimalScaleAttributeTests
    {
        [Theory]
        [InlineData(1)]
        [InlineData(1.1)]
        public void ValueWithinScale_IsAccepted_AtScale1(decimal value) =>
            Assert.True(new DecimalScaleAttribute(1).IsValid(value));

        [Fact]
        public void ValueWithMoreThanOneDecimalPlace_IsRejected_AtScale1() =>
            // Mirrors Multa.Taxa, serialized by DecimalF1Converter ("F1"): scale 1, not 2.
            Assert.False(new DecimalScaleAttribute(1).IsValid(0.45m));

        [Theory]
        [InlineData(1)]
        [InlineData(1.1)]
        [InlineData(1.12)]
        public void ValueWithinScale_IsAccepted_AtScale2(decimal value) =>
            Assert.True(new DecimalScaleAttribute(2).IsValid(value));

        [Fact]
        public void ValueWithMoreThanTwoDecimalPlaces_IsRejected_AtScale2() =>
            // Mirrors ValorNominal and most money fields, serialized by DecimalF2Converter ("F2").
            Assert.False(new DecimalScaleAttribute(2).IsValid(1234.567m));

        [Theory]
        [InlineData(1)]
        [InlineData(1.1)]
        [InlineData(1.12)]
        [InlineData(1.123)]
        public void ValueWithinScale_IsAccepted_AtScale3(decimal value) =>
            Assert.True(new DecimalScaleAttribute(3).IsValid(value));

        [Fact]
        public void ValueWithMoreThanThreeDecimalPlaces_IsRejected_AtScale3() =>
            // Mirrors BeneficiarioRateio.Percentual, serialized by DecimalF3Converter ("F3"): scale 3, not 4.
            Assert.False(new DecimalScaleAttribute(3).IsValid(16.2534m));

        [Fact]
        public void Null_IsAccepted() =>
            // Absence is [Required]'s business, not a scale's.
            Assert.True(new DecimalScaleAttribute(2).IsValid(null));

        [Fact]
        public void NonDecimalValue_IsRejected() =>
            // A non-decimal reaching a decimal-typed property means the model changed; the attribute must
            // fail closed rather than throw out of validation.
            Assert.False(new DecimalScaleAttribute(2).IsValid("1.23"));

        [Fact]
        public void TrailingZerosAtTheDeclaredScale_AreAccepted()
        {
            // decimal keeps track of trailing zeros internally (1.10m and 1.1m are equal but not identical in
            // representation), which makes this look like it should fail at a glance. It does not:
            // decimal.Round(1.10m, 1) returns 1.1m, and 1.1m == 1.10m is true by value, which is exactly what
            // IsValid relies on - so a value that merely carries a redundant trailing zero at the declared
            // scale is accepted, not rejected.
            Assert.True(new DecimalScaleAttribute(1).IsValid(1.10m));
            Assert.Equal(1.1m, decimal.Round(1.10m, 1));
        }
    }
}
