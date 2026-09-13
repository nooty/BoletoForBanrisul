using BoletoForBanrisul.Core.Models;
using BoletoForBanrisul.Core.Models.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoletoForBanrisul.Report
{
    /// <summary>
    /// Settings for the content of a printed boleto.
    /// </summary>
    /// <remarks>
    /// These are data that can vary from one boleto to the next. The template's origin is not here:
    /// that is application configuration and lives in <see cref="IBoletoTemplateRenderer"/>.
    /// </remarks>
    public class PrintSettings
    {
        /// <summary>
        /// Path to the file holding the boleto's logo.
        /// </summary>
        public string LogoFilePath { get; set; } = "";

        /// <summary>
        /// Mask for the generated PDF's file name.
        /// Placeholders are replaced with the boleto's actual field values.
        ///
        /// Available placeholders:
        /// - {NossoNumero}: the boleto's unique identifier.
        /// - {SeuNumero}: reference number set by the client.
        /// - {CpfCnpjPagador}: the pagador's CPF or CNPJ (letters and digits only).
        /// - {NomePagador}: the pagador's name.
        /// - {DataVencimento}: the boleto's due date, formatted "yyyyMMdd".
        /// - {DataEmissao}: the boleto's issue date, formatted "yyyyMMdd".
        /// - {NomeBeneficiario}: the beneficiário's name (who receives the payment).
        /// - {CpfCnpjBeneficiario}: the beneficiário's CPF or CNPJ (letters and digits only).
        ///
        /// Example: "boleto_{NossoNumero}_{DataVencimento}" yields something like "boleto_12345_20220301.pdf".
        /// </summary>
        public string FileNameTemplate { get; set; } = "boleto_{NossoNumero}";

        /// <summary>
        /// Payment location shown on the boleto.
        /// </summary>
        public string PaymentLocation { get; set; } = "PAGAR EM QUALQUER BANCO OU CORRESPONDENTE BANCÁRIO.";

        /// <summary>
        /// The beneficiário settings used on the boleto.
        /// </summary>
        public BeneficiarySettings Beneficiary { get; set; } = new BeneficiarySettings();
    }

    public class BeneficiarySettings
    {
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public string CpfCnpj { get; set; } = "";
        public string Address { get; set; } = "";
        public string City { get; set; } = "";
        public string State { get; set; } = "";
        public string ZipCode { get; set; } = "";
    }
}
