using BoletoForBanrisul.Core.Helpers;
using BoletoForBanrisul.Core.Models;
using BoletoForBanrisul.Core.Models.Types;
using System.Globalization;

namespace BoletoForBanrisul.Core.Tests.Helpers
{
    [Trait("Category", "Unit")]
    public class InstructionHelperTests
    {
        [Fact]
        public void ConvertInstructionsToMessages_JurosIsento_ProducesNoJurosMessage()
        {
            // Isento is the default and is deliberately not cross-checked (see the XML doc on Juros.Codigo);
            // it also never renders a message.
            var instructions = new Instrucoes { Juros = new Juros { Codigo = CodigoJuros.Isento } };

            var messages = InstructionHelper.ConvertInstructionsToMessages(instructions);

            Assert.Empty(messages);
        }

        [Fact]
        public void ConvertInstructionsToMessages_JurosTaxaMensal_RendersThePercentageInPtBr()
        {
            var instructions = new Instrucoes { Juros = new Juros { Codigo = CodigoJuros.TaxaMensal, Taxa = 1.5m } };

            var messages = InstructionHelper.ConvertInstructionsToMessages(instructions);

            var message = Assert.Single(messages);
            Assert.Equal(1, message.Linha);
            Assert.Equal("1) Juros: Cobrar juros mensais de 1,50% após o vencimento.", message.Texto);
        }

        [Fact]
        public void ConvertInstructionsToMessages_JurosValorPorDia_WithDate_RendersTheDateAndPtBrAmount()
        {
            var instructions = new Instrucoes
            {
                Juros = new Juros { Codigo = CodigoJuros.ValorPorDia, Valor = 2.5m, Data = new DateTime(2026, 9, 1) }
            };

            var messages = InstructionHelper.ConvertInstructionsToMessages(instructions);

            var message = Assert.Single(messages);
            Assert.Equal("1) Juros: Cobrar juros por dia de atraso no valor de R$ 2,50 a partir de 01/09/2026.", message.Texto);
        }

        [Fact]
        public void ConvertInstructionsToMessages_MultaPercentual_UsesOneDecimalPlace()
        {
            var instructions = new Instrucoes
            {
                Multa = new Multa { Codigo = CodigoMulta.Percentual, Taxa = 0.4m }
            };

            var messages = InstructionHelper.ConvertInstructionsToMessages(instructions);

            var message = Assert.Single(messages);
            Assert.Equal(1, message.Linha);
            Assert.Equal("1) Multa: Multa de 0,4% após o vencimento.", message.Texto);
        }

        [Fact]
        public void ConvertInstructionsToMessages_DescontoValorFixoAteData_RendersTheDateAndValue()
        {
            var instructions = new Instrucoes
            {
                Desconto = new Desconto { Codigo = CodigoDesconto.ValorFixoAteData, Valor = 6.95m, Data = new DateTime(2026, 8, 15) }
            };

            var messages = InstructionHelper.ConvertInstructionsToMessages(instructions);

            var message = Assert.Single(messages);
            Assert.Equal("1) Desconto: Desconto de R$ 6,95 até 15/08/2026.", message.Texto);
        }

        [Fact]
        public void ConvertInstructionsToMessages_ProtestoProtestarDiasCorridos_RendersTheDeadline()
        {
            var instructions = new Instrucoes
            {
                Protesto = new Protesto { Codigo = CodigoProtesto.ProtestarDiasCorridos, Prazo = 5 }
            };

            var messages = InstructionHelper.ConvertInstructionsToMessages(instructions);

            var message = Assert.Single(messages);
            Assert.Equal("1) Protesto: Protestar após 5 dias corridos do vencimento.", message.Texto);
        }

        [Fact]
        public void ConvertInstructionsToMessages_ProtestoWithACodeOtherThanProtestarDiasCorridos_ProducesNoMessage()
        {
            // Only ProtestarDiasCorridos renders a message; NegativarDiasCorridos and NaoProtestar/NaoNegativar
            // are silent here even though they are valid Protesto.Codigo values.
            var instructions = new Instrucoes
            {
                Protesto = new Protesto { Codigo = CodigoProtesto.NaoProtestar }
            };

            var messages = InstructionHelper.ConvertInstructionsToMessages(instructions);

            Assert.Empty(messages);
        }

        [Fact]
        public void ConvertInstructionsToMessages_AbatimentoWithAPositiveValue_RendersTheAmount()
        {
            var instructions = new Instrucoes { Abatimento = new Abatimento { Valor = 6.80m } };

            var messages = InstructionHelper.ConvertInstructionsToMessages(instructions);

            var message = Assert.Single(messages);
            Assert.Equal("1) Abatimento: Conceder abatimento de valor fixo de R$ 6,80.", message.Texto);
        }

        [Fact]
        public void ConvertInstructionsToMessages_AbatimentoWithAZeroValue_ProducesNoMessage()
        {
            var instructions = new Instrucoes { Abatimento = new Abatimento { Valor = 0m } };

            var messages = InstructionHelper.ConvertInstructionsToMessages(instructions);

            Assert.Empty(messages);
        }

        [Fact]
        public void ConvertInstructionsToMessages_BaixaWithAPositivePrazo_RendersTheDeadline()
        {
            var instructions = new Instrucoes { Baixa = new Baixa { Codigo = CodigoBaixa.BaixarDevolver, Prazo = 30 } };

            var messages = InstructionHelper.ConvertInstructionsToMessages(instructions);

            var message = Assert.Single(messages);
            Assert.Equal("1) Baixa: Baixar/Devolver após 30 dias corridos do vencimento para o Beneficiário.", message.Texto);
        }

        [Fact]
        public void ConvertInstructionsToMessages_BaixaWithAZeroPrazo_ProducesNoMessage()
        {
            var instructions = new Instrucoes { Baixa = new Baixa { Codigo = CodigoBaixa.BaixarDevolver, Prazo = 0 } };

            var messages = InstructionHelper.ConvertInstructionsToMessages(instructions);

            Assert.Empty(messages);
        }

        [Fact]
        public void ConvertInstructionsToMessages_MultipleInstructions_NumbersLinesInEncounterOrder()
        {
            var instructions = new Instrucoes
            {
                Juros = new Juros { Codigo = CodigoJuros.TaxaMensal, Taxa = 1.0m },
                Multa = new Multa { Codigo = CodigoMulta.Percentual, Taxa = 0.4m },
                Abatimento = new Abatimento { Valor = 6.80m }
            };

            var messages = InstructionHelper.ConvertInstructionsToMessages(instructions);

            Assert.Equal(3, messages.Count);
            Assert.Equal(new[] { 1, 2, 3 }, messages.Select(m => m.Linha));
            Assert.StartsWith("1) Juros:", messages[0].Texto);
            Assert.StartsWith("2) Multa:", messages[1].Texto);
            Assert.StartsWith("3) Abatimento:", messages[2].Texto);
        }

        [Fact]
        public void ConvertInstructionsToMessages_UsesPtBrFormatting_RegardlessOfTheCurrentCulture()
        {
            // InstructionHelper fixes a pt-BR CultureInfo internally so messages read correctly regardless of
            // the culture the process happens to run under; this proves it does not leak the ambient
            // culture's decimal separator convention.
            var previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");

                var instructions = new Instrucoes { Juros = new Juros { Codigo = CodigoJuros.TaxaMensal, Taxa = 1.5m } };
                var messages = InstructionHelper.ConvertInstructionsToMessages(instructions);

                Assert.Contains("1,50%", Assert.Single(messages).Texto);
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
        }
    }
}
