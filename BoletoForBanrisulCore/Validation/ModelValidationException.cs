using System.ComponentModel.DataAnnotations;

namespace BoletoForBanrisul.Core.Validation
{
    /// <summary>
    /// Thrown by <see cref="RecursiveValidator.ValidateAndThrow"/> when an object graph fails validation.
    /// Unlike a bare <see cref="ValidationException"/> (which carries a single failure), this exposes the
    /// full list of <see cref="ValidationResult"/>s found across the whole graph, each with a
    /// <see cref="ValidationResult.MemberNames"/> entry set to the dotted path of the offending member
    /// (e.g. <c>Titulo.Pagador.Cep</c>, <c>Titulo.Instrucoes.Desconto.Valor</c>, <c>Titulo.Rateio.Beneficiarios[1].Codigo</c>).
    /// </summary>
    /// <remarks>
    /// Named for the model rather than for the bank slip specifically because <see cref="RecursiveValidator"/>
    /// is generic over any annotated object graph and knows nothing about which model it was handed.
    /// </remarks>
    public class ModelValidationException : Exception
    {
        /// <summary>Every validation error found in the object graph, in the order they were discovered.</summary>
        public IReadOnlyList<ValidationResult> Errors { get; }

        /// <summary>Creates an exception carrying the given list of validation errors.</summary>
        /// <param name="errors">The validation errors to report. Must not be empty.</param>
        public ModelValidationException(IReadOnlyList<ValidationResult> errors)
            : base(BuildMessage(errors))
        {
            Errors = errors;
        }

        private static string BuildMessage(IReadOnlyList<ValidationResult> errors)
        {
            if (errors is null || errors.Count == 0)
                return "Validation failed.";

            var lines = errors.Select(e =>
            {
                string path = e.MemberNames?.FirstOrDefault() ?? "(object)";
                return $"{path}: {e.ErrorMessage}";
            });

            return $"Validation failed with {errors.Count} error(s):{Environment.NewLine}" +
                   string.Join(Environment.NewLine, lines.Select(l => "  - " + l));
        }
    }
}
