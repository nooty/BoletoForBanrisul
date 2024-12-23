using BoletoForBanrisul.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.Json;
using BoletoForBanrisul.Core.Models.Types;

namespace BoletoForBanrisul.Core.Converters
{
    public class SituacaoBanrisulConverter : JsonConverter<SituacaoBanrisul?>
    {
        public override SituacaoBanrisul? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
                return null;

            string situacao = reader.GetString() ?? "";

            return situacao switch
            {
                "A" => SituacaoBanrisul.Ativo,
                "B" => SituacaoBanrisul.BaixadoPorPagamento,
                "D" => SituacaoBanrisul.BaixadoPorDevolucao,
                "L" => SituacaoBanrisul.Liquidado,
                "R" => SituacaoBanrisul.Reembolsado,
                "T" => SituacaoBanrisul.TransferidoParaCL,
                "P" => SituacaoBanrisul.Protestado,
                _ => throw new JsonException($"Valor inválido para SituacaoBanrisul: {situacao}.")
            };
        }

        public override void Write(Utf8JsonWriter writer, SituacaoBanrisul? value, JsonSerializerOptions options)
        {
            if (value.HasValue)
            {
                char situacao = value.Value switch
                {
                    SituacaoBanrisul.Ativo => 'A',
                    SituacaoBanrisul.BaixadoPorPagamento => 'B',
                    SituacaoBanrisul.BaixadoPorDevolucao => 'D',
                    SituacaoBanrisul.Liquidado => 'L',
                    SituacaoBanrisul.Reembolsado => 'R',
                    SituacaoBanrisul.TransferidoParaCL => 'T',
                    SituacaoBanrisul.Protestado => 'P',
                    _ => throw new JsonException($"Valor inválido para SituacaoBanrisul: {value}.")
                };
                writer.WriteStringValue(situacao.ToString());
                return;
            }

            writer.WriteNullValue();
        }
    }
}
