using System.ComponentModel.DataAnnotations;
using MiniValidation;

namespace BoletoForBanrisul.Core.Validation
{
    /// <summary>
    /// Validates an object graph recursively using <see cref="System.ComponentModel.DataAnnotations"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="Validator.TryValidateObject(object, ValidationContext, ICollection{ValidationResult}, bool)"/>
    /// only looks at the properties of the single object it is given: a bad <c>Titulo.Pagador.Cep</c> or a
    /// bad <c>Titulo.Instrucoes.Desconto.Valor</c> passes silently unless something walks into those nested
    /// objects. The walk itself is delegated to <see cref="MiniValidator"/>, which descends into every
    /// complex reference-typed property and into the elements of every collection, running both attribute
    /// validation and <see cref="IValidatableObject.Validate"/> at each node, and aggregating <b>every</b>
    /// error found anywhere in the graph instead of stopping at the first failure.
    /// </para>
    /// <para>
    /// Delegating matters for one behaviour in particular. <c>Validator.TryValidateObject</c> invokes
    /// <see cref="IValidatableObject.Validate"/> only when every attribute on the object already passed,
    /// which would silently drop the cross-field rules on <see cref="Models.Titulo"/> and its nested
    /// instructions whenever a <c>[Required]</c> was also missing. MiniValidation avoids that the same way
    /// the hand-written walker this replaced did: attributes are checked per property with
    /// <c>Validator.TryValidateValue</c>, and <c>Validate()</c> is then called unconditionally.
    /// </para>
    /// <para>
    /// This type remains the public surface so callers depend on <see cref="ValidationResult"/> and
    /// <see cref="ModelValidationException"/> rather than on MiniValidation's error dictionary. Each
    /// reported result carries the dotted path to the offending member as its single
    /// <see cref="ValidationResult.MemberNames"/> entry, e.g. <c>Titulo.Pagador.Cep</c>,
    /// <c>Titulo.Instrucoes.Desconto.Valor</c> or <c>Titulo.Rateio.Beneficiarios[1].Codigo</c>. A rule that
    /// blames several members at once is reported once per member, each with its own path, because the
    /// underlying errors are keyed by member; that is also the shape a caller mapping errors back onto form
    /// fields wants.
    /// </para>
    /// </remarks>
    public static class RecursiveValidator
    {
        /// <summary>
        /// The path reported for a class-level error on the root object, which has no member name of its own.
        /// </summary>
        private const string RootPath = "(object)";

        /// <summary>
        /// Validates <paramref name="instance"/> and its entire object graph, collecting every error found.
        /// </summary>
        /// <param name="instance">The root object to validate. <c>null</c> is treated as valid.</param>
        /// <param name="errors">
        /// Every <see cref="ValidationResult"/> found anywhere in the graph, or an empty list when the graph
        /// is valid. Never <c>null</c>.
        /// </param>
        /// <returns><c>true</c> when the graph is valid; <c>false</c> when <paramref name="errors"/> is non-empty.</returns>
        public static bool TryValidate(object? instance, out IReadOnlyList<ValidationResult> errors)
        {
            var results = new List<ValidationResult>();
            errors = results;

            if (instance is null)
                return true;

            if (MiniValidator.TryValidate(instance, recurse: true, out var keyedErrors))
                return true;

            foreach (var error in keyedErrors)
            {
                // MiniValidation strips the trailing separator from a class-level key, so a rule reported by
                // a nested object's own Validate() already arrives as "Rateio[1]"; only the root's is empty.
                string path = error.Key.Length == 0 ? RootPath : error.Key;

                foreach (var message in error.Value)
                    results.Add(new ValidationResult(message, new[] { path }));
            }

            return results.Count == 0;
        }

        /// <summary>
        /// Validates <paramref name="instance"/> and its entire object graph, throwing when any error is found.
        /// </summary>
        /// <param name="instance">The root object to validate.</param>
        /// <exception cref="ModelValidationException">
        /// Thrown with the full list of errors found in the graph when validation fails.
        /// </exception>
        public static void ValidateAndThrow(object instance)
        {
            if (!TryValidate(instance, out var errors))
                throw new ModelValidationException(errors);
        }
    }
}
