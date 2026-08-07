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
    /// Configurações do conteúdo de um boleto impresso.
    /// </summary>
    /// <remarks>
    /// São dados que podem variar de um boleto para o outro. A origem do template não está aqui:
    /// ela é configuração de aplicação e vive no <see cref="IBoletoTemplateRenderer"/>.
    /// </remarks>
    public class PrintSettings
    {
        /// <summary>
        /// Caminho para o arquivo onde o logotipo do boleto está armazenado.
        /// </summary>
        public string LogoFilePath { get; set; } = "";

        /// <summary>
        /// Máscara para o nome do arquivo PDF gerado. 
        /// Substitui os placeholders pelos valores reais dos campos do boleto.
        /// 
        /// Placeholders disponíveis:
        /// - {NossoNumero}: Número de identificação único do boleto.
        /// - {SeuNumero}: Número de referência definido pelo cliente.
        /// - {CpfCnpjPagador}: CPF ou CNPJ do pagador (somente números).
        /// - {NomePagador}: Nome do pagador.
        /// - {DataVencimento}: Data de vencimento do boleto, no formato "yyyyMMdd".
        /// - {DataEmissao}: Data de emissão do boleto, no formato "yyyyMMdd".
        /// - {NomeBeneficiario}: Nome do beneficiário (quem recebe o pagamento).
        /// - {CpfCnpjBeneficiario}: CPF ou CNPJ do beneficiário (somente números).
        /// 
        /// Exemplo: "boleto_{NossoNumero}_{DataVencimento}" geraria algo como "boleto_12345_20220301.pdf".
        /// </summary>
        public string FileNameTemplate { get; set; } = "boleto_{NossoNumero}";

        /// <summary>
        /// Local de pagamento exibido no boleto.
        /// </summary>
        public string PaymentLocation { get; set; } = "PAGAR EM QUALQUER BANCO OU CORRESPONDENTE BANCÁRIO.";

        /// <summary>
        /// Configurações do beneficiário que serão utilizadas no boleto.
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
