using System.ComponentModel.DataAnnotations;

namespace BoletoForBanrisul.Core.Validation
{
    /// <summary>
    /// Rejects a <see cref="decimal"/> (or <see cref="Nullable{T}"/> of one) that carries more decimal
    /// places than <see cref="Scale"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every decimal property on this model is serialized by
    /// <see cref="Converters.DecimalF1Converter"/>, <see cref="Converters.DecimalF2Converter"/> or
    /// <see cref="Converters.DecimalF3Converter"/>, which format the value with a fixed <c>"F1"</c>,
    /// <c>"F2"</c> or <c>"F3"</c> respectively. A value carrying more precision than the converter writes is
    /// therefore <b>silently rounded on the wire</b>: <c>Multa.Taxa = 0.45m</c> is written as <c>"0.5"</c> by
    /// <see cref="Converters.DecimalF1Converter"/>, and <c>BeneficiarioRateio.Percentual = 16.2534m</c> is
    /// written as <c>"16.253"</c> by <see cref="Converters.DecimalF3Converter"/>. Any local comparison or sum
    /// performed against the unrounded in-memory value — a rateio percentage sum, an abatement compared
    /// against the nominal value — then runs on numbers the bank never actually receives, which can pass
    /// locally and still be rejected (or silently misread) by the bank's own equivalent check.
    /// </para>
    /// <para>
    /// Rejecting the extra precision up front keeps the local view and the wire view in agreement, and
    /// follows this library's standing preference for a visible local failure over a silent change to the
    /// caller's data (the same preference that drove replacing <c>[TruncateString]</c> with
    /// <see cref="StringLengthAttribute"/> elsewhere in this model).
    /// </para>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public sealed class DecimalScaleAttribute : ValidationAttribute
    {
        /// <summary>Creates the attribute for the given maximum number of decimal places.</summary>
        /// <param name="scale">The maximum number of decimal places accepted, matching the converter's fixed format ("F1" → 1, "F2" → 2, "F3" → 3).</param>
        public DecimalScaleAttribute(int scale)
        {
            Scale = scale;
        }

        /// <summary>The maximum number of decimal places accepted.</summary>
        public int Scale { get; }

        /// <inheritdoc />
        public override bool IsValid(object? value)
        {
            if (value is null)
                return true;

            if (value is decimal d)
                return decimal.Round(d, Scale) == d;

            // A non-decimal reaching a decimal-typed property means the model changed; let the caller see a
            // failed validation rather than an exception out of the validation layer.
            return false;
        }

        /// <inheritdoc />
        public override string FormatErrorMessage(string name) =>
            string.Format(
                System.Globalization.CultureInfo.CurrentCulture,
                ErrorMessage ?? "The field {0} must not carry more than {1} decimal place(s): it is serialized with exactly {1}, so additional precision would be silently rounded away before the bank sees it.",
                name,
                Scale);
    }
}
