using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace BoletoForBanrisul.Core.Validation
{
    /// <summary>
    /// Bounds a <see cref="decimal"/> (or <see cref="Nullable{T}"/> of one) between an inclusive minimum and
    /// maximum, replacing <see cref="RangeAttribute"/> for every money-like field in this library.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="RangeAttribute"/> is not usable here, for two independent reasons that both bite in
    /// production rather than in a unit test.
    /// </para>
    /// <para>
    /// <b>It is not thread-safe on first use.</b> Its <c>decimal</c> form takes its bounds as
    /// <see cref="string"/> and converts them lazily, on the first call to <c>IsValid</c>, by overwriting its
    /// own <c>Minimum</c>/<c>Maximum</c> properties in place. Validation attributes are cached and reused, so
    /// a single instance is shared by every thread that validates that property. Two threads reaching the
    /// conversion together — an application registering bank slips concurrently, which is the normal case —
    /// can have one of them read a bound the other has already converted and throw
    /// <see cref="InvalidCastException"/> ("Unable to cast object of type 'System.Decimal' to type
    /// 'System.String'") out of validation. This library has more decimal-typed properties than most
    /// (roughly twenty across <c>Titulo</c> and its nested instructions), so it has more attribute instances
    /// racing on first use, not fewer.
    /// </para>
    /// <para>
    /// <b>Its string bounds are parsed with the current culture.</b> Under any comma-decimal locale — pt-BR,
    /// this library's own audience — <c>"999999999999.99"</c> throws <see cref="ArgumentException"/> instead
    /// of validating, unless <c>ParseLimitsInInvariantCulture</c> is remembered at every single usage.
    /// </para>
    /// <para>
    /// Both problems come from the same root: bounds arriving as text and being converted at validation time.
    /// This attribute takes them as <see cref="decimal"/> literals instead, so there is nothing to parse,
    /// nothing to mutate, and no shared state to race on — the instance is immutable after construction.
    /// </para>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public sealed class DecimalRangeAttribute : ValidationAttribute
    {
        /// <summary>Creates the attribute for an inclusive <paramref name="minimum"/>..<paramref name="maximum"/> range.</summary>
        /// <param name="minimum">Smallest accepted value, inclusive, written in invariant form (<c>"0"</c>).</param>
        /// <param name="maximum">Largest accepted value, inclusive, written in invariant form (<c>"999.99"</c>).</param>
        /// <exception cref="ArgumentException">A bound is not a valid invariant-culture decimal.</exception>
        /// <remarks>
        /// The bounds arrive as text because an attribute argument cannot be a <see cref="decimal"/> constant,
        /// and as text rather than <see cref="double"/> because <c>999999999999.99</c> has more significant
        /// digits than a <see cref="double"/> holds exactly. Unlike <see cref="RangeAttribute"/>, the parse
        /// happens exactly once — here, at construction, against
        /// <see cref="CultureInfo.InvariantCulture"/> — so no validation call can observe a half-converted
        /// bound and no locale can change the result.
        /// </remarks>
        public DecimalRangeAttribute(string minimum, string maximum)
        {
            Minimum = Parse(minimum, nameof(minimum));
            Maximum = Parse(maximum, nameof(maximum));
        }

        private static decimal Parse(string bound, string parameterName)
        {
            if (!decimal.TryParse(bound, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal parsed))
                throw new ArgumentException($"'{bound}' is not a valid invariant-culture decimal.", parameterName);

            return parsed;
        }

        /// <summary>Smallest accepted value, inclusive.</summary>
        public decimal Minimum { get; }

        /// <summary>Largest accepted value, inclusive.</summary>
        public decimal Maximum { get; }

        /// <inheritdoc />
        public override bool IsValid(object? value)
        {
            if (value is null)
                return true;

            if (value is decimal d)
                return d >= Minimum && d <= Maximum;

            // A non-decimal reaching a decimal-typed property means the model changed; let the caller see a
            // failed validation rather than an exception out of the validation layer.
            return false;
        }

        /// <inheritdoc />
        public override string FormatErrorMessage(string name) =>
            string.Format(
                CultureInfo.CurrentCulture,
                ErrorMessage ?? "The field {0} must be between {1} and {2}.",
                name,
                Minimum.ToString(CultureInfo.CurrentCulture),
                Maximum.ToString(CultureInfo.CurrentCulture));
    }
}
