using System.ComponentModel.DataAnnotations;
using MiniValidation;

namespace BoletoForBanrisul.Core.Validation
{
    /// <summary>
    /// Validates an object graph recursively using <see cref="System.ComponentModel.DataAnnotations"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The walk is delegated to <see cref="MiniValidator"/>, which descends into nested objects and
    /// collections and aggregates every error in the graph. Two behaviours it is chosen for:
    /// <c>Validator.TryValidateObject</c> does not recurse at all, and it runs
    /// <see cref="IValidatableObject.Validate"/> only when every attribute passed — which would drop the
    /// cross-field rules whenever a <c>[Required]</c> was also missing. MiniValidation calls
    /// <c>Validate()</c> unconditionally instead.
    /// </para>
    /// <para>
    /// This type stays the public surface so callers depend on <see cref="ValidationResult"/> rather than on
    /// MiniValidation's error dictionary. Each result carries the dotted path to the member as its single
    /// <see cref="ValidationResult.MemberNames"/> entry (<c>Titulo.Pagador.Cep</c>,
    /// <c>Titulo.Rateio.Beneficiarios[1].Codigo</c>); a rule blaming several members is reported once per
    /// member, since the underlying errors are keyed by member.
    /// </para>
    /// </remarks>
    public static class RecursiveValidator
    {
        /// <summary>Path reported for a class-level error on the root object, which has no member name.</summary>
        private const string RootPath = "(object)";

        /// <summary>
        /// Validates <paramref name="instance"/> and its entire object graph, collecting every error found.
        /// </summary>
        /// <param name="instance">The root object to validate. <c>null</c> is treated as valid.</param>
        /// <param name="errors">Every error found in the graph, empty when valid. Never <c>null</c>.</param>
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
                // MiniValidation strips the trailing separator from a class-level key, so only the root's is empty.
                string path = error.Key.Length == 0 ? RootPath : error.Key;

                foreach (var message in error.Value)
                    results.Add(new ValidationResult(message, new[] { path }));
            }

            return results.Count == 0;
        }

        /// <summary>Validates the object graph, throwing when any error is found.</summary>
        /// <param name="instance">The root object to validate.</param>
        /// <exception cref="ModelValidationException">Thrown with every error found in the graph.</exception>
        public static void ValidateAndThrow(object instance)
        {
            if (!TryValidate(instance, out var errors))
                throw new ModelValidationException(errors);
        }
    }
}
