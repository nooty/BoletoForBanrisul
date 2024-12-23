using BoletoForBanrisul.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.Json;
using BoletoForBanrisul.Core.Models.Types;

namespace BoletoForBanrisul.Core.Converters
{
    public class SituacaoCipConverter : JsonConverter<SituacaoCip?>
    {
        public override SituacaoCip? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
                return null;

            string situacao = reader.GetString() ?? "";

            return situacao switch
            {
                "A" => SituacaoCip.Aprovado,
                "R" => SituacaoCip.Rejeitado,
                "P" => SituacaoCip.Pendente,
                "E" => SituacaoCip.Enviado,
                "N" => SituacaoCip.NaoCentralizado,
                _ => throw new JsonException($"Valor inválido para SituacaoCip: {situacao}.")
            };
        }

        public override void Write(Utf8JsonWriter writer, SituacaoCip? value, JsonSerializerOptions options)
        {
            if (value.HasValue)
            {
                char situacao = value.Value switch
                {
                    SituacaoCip.Aprovado => 'A',
                    SituacaoCip.Rejeitado => 'R',
                    SituacaoCip.Pendente => 'P',
                    SituacaoCip.Enviado => 'E',
                    SituacaoCip.NaoCentralizado => 'N',
                    _ => throw new JsonException($"Valor inválido para SituacaoCip: {value}.")
                };
                writer.WriteStringValue(situacao.ToString());
                return;
            }

            writer.WriteNullValue();
        }
    }

}
