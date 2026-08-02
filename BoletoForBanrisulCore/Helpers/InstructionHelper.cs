using BoletoForBanrisul.Core.Models;
using BoletoForBanrisul.Core.Models.Types;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoletoForBanrisul.Core.Helpers
{
    public class InstructionHelper
    {
        // As mensagens do boleto são sempre em pt-BR; a cultura é fixada para que valores e
        // datas não variem conforme a cultura da máquina em que o código executa.
        private static readonly CultureInfo PtBrCulture = CultureInfo.GetCultureInfo("pt-BR");

        // "1 dia corrido" vs. "N dias corridos": Baixa.Prazo aceita 1 ([Range(0, 99)]) e a mensagem é
        // renderizada para qualquer Prazo > 0.
        private static string DaysPhrase(int days) => days == 1 ? "1 dia corrido" : $"{days} dias corridos";

        public static List<Mensagem> ConvertInstructionsToMessages(Instrucoes instructions)
        {
            var messages = new List<Mensagem>();
            int line = 1;

            // Mensagem de Juros.
            if (instructions.Juros.Codigo != CodigoJuros.Isento)
            {
                var jurosMsg = new StringBuilder($"{line}) Juros: ");
                string dataReferencia = instructions.Juros.Data.HasValue
                    ? $"a partir de {instructions.Juros.Data.Value.ToString("dd/MM/yyyy", PtBrCulture)}."
                    : "após o vencimento.";

                switch (instructions.Juros.Codigo)
                {
                    case CodigoJuros.ValorPorDia:
                        if (instructions.Juros.Valor.HasValue)
                        {
                            jurosMsg.Append($"Cobrar juros por dia de atraso no valor de R$ {instructions.Juros.Valor.Value.ToString("F2", PtBrCulture)} {dataReferencia}");
                        }
                        break;
                    case CodigoJuros.TaxaMensal:
                        if (instructions.Juros.Taxa.HasValue)
                        {
                            jurosMsg.Append($"Cobrar juros mensais de {instructions.Juros.Taxa.Value.ToString("F2", PtBrCulture)}% {dataReferencia}");
                        }
                        break;
                }

                messages.Add(new Mensagem { Linha = line++, Texto = jurosMsg.ToString() });
            }

            // Mensagem de Multa.
            if (instructions.Multa != null)
            {
                var multaMsg = new StringBuilder($"{line}) Multa: ");
                string dataReferencia = instructions.Multa.Data.HasValue
                    ? $"a partir de {instructions.Multa.Data.Value.ToString("dd/MM/yyyy", PtBrCulture)}."
                    : "após o vencimento.";

                switch (instructions.Multa.Codigo)
                {
                    case CodigoMulta.ValorFixo:
                        if (instructions.Multa.Valor.HasValue)
                        {
                            multaMsg.Append($"Multa de valor fixo de R$ {instructions.Multa.Valor.Value.ToString("F2", PtBrCulture)} {dataReferencia}");
                        }
                        break;
                    case CodigoMulta.Percentual:
                        if (instructions.Multa.Taxa.HasValue)
                        {
                            multaMsg.Append($"Multa de {instructions.Multa.Taxa.Value.ToString("F1", PtBrCulture)}% {dataReferencia}");
                        }
                        break;
                }

                messages.Add(new Mensagem { Linha = line++, Texto = multaMsg.ToString() });
            }

            // Mensagem de Desconto.
            if (instructions.Desconto != null)
            {
                var dataReferencia = instructions.Desconto.Data.HasValue ? $"{instructions.Desconto.Data.Value.ToString("dd/MM/yyyy", PtBrCulture)}." : "a data do vencimento.";

                var descontoMsg = new StringBuilder($"{line}) Desconto: ");
                switch (instructions.Desconto.Codigo)
                {
                    case CodigoDesconto.ValorFixoAteData:
                        descontoMsg.Append($"Desconto de R$ {instructions.Desconto.Valor?.ToString("F2", PtBrCulture)} até {dataReferencia}");
                        break;
                    case CodigoDesconto.PercentualAteData:
                        descontoMsg.Append($"Desconto de {instructions.Desconto.Taxa?.ToString("F2", PtBrCulture)}% até {dataReferencia}");
                        break;
                    case CodigoDesconto.ValorPorAntecipacaoDiaCorrido:
                        descontoMsg.Append($"Desconto por antecipação de R$ {instructions.Desconto.Valor?.ToString("F2", PtBrCulture)} por dia corrido");
                        break;
                    case CodigoDesconto.PercentualSobreValorNominalDiaCorrido:
                        descontoMsg.Append($"Desconto de {instructions.Desconto.Taxa?.ToString("F2", PtBrCulture)}% sobre o valor nominal por dia corrido");
                        break;
                }

                messages.Add(new Mensagem { Linha = line++, Texto = descontoMsg.ToString() });
            }

            // Mensagem de Protesto.
            if (instructions.Protesto != null && instructions.Protesto.Codigo == CodigoProtesto.ProtestarDiasCorridos)
            {
                var protestoMsg = new StringBuilder($"{line}) Protesto: ");

                if (!instructions.Protesto.Prazo.HasValue)
                {
                    protestoMsg.Append("Protestar após o vencimento.");
                }
                else if (instructions.Protesto.Prazo.Value == 0)
                {
                    protestoMsg.Append("Protestar imediatamente após o vencimento.");
                }
                else
                {
                    protestoMsg.Append($"Protestar após {DaysPhrase(instructions.Protesto.Prazo.Value)} do vencimento.");
                }

                messages.Add(new Mensagem { Linha = line++, Texto = protestoMsg.ToString() });
            }

            // Mensagem de Abatimento.
            if (instructions.Abatimento != null && instructions.Abatimento.Valor > 0)
            {
                var abatimentoMsg = new StringBuilder($"{line}) Abatimento: ");

                abatimentoMsg.Append($"Conceder abatimento de valor fixo de R$ {instructions.Abatimento.Valor.ToString("F2", PtBrCulture)}.");

                messages.Add(new Mensagem { Linha = line++, Texto = abatimentoMsg.ToString() });
            }

            // Mensagem de Baixa.
            if (instructions.Baixa != null && instructions.Baixa.Prazo > 0)
            {
                var baixaMsg = new StringBuilder($"{line}) Baixa: ");

                baixaMsg.Append($"Baixar/Devolver após {DaysPhrase(instructions.Baixa.Prazo)} do vencimento para o Beneficiário.");

                messages.Add(new Mensagem { Linha = line++, Texto = baixaMsg.ToString() });
            }

            return messages;
        }
    }

}
