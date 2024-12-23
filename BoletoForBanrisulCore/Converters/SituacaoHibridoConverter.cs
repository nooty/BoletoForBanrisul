using BoletoForBanrisul.Core.Models;
using BoletoForBanrisul.Core.Models.Types;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BoletoForBanrisul.Core.Converters
{
    public class SituacaoHibridoConverter : JsonConverter<SituacaoHibrido>
    {
        public override SituacaoHibrido Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            string value = reader.GetString() ?? "";
            return value switch
            {
                "A" => SituacaoHibrido.Criado,
                "N" => SituacaoHibrido.NaoCriado,
                "P" => SituacaoHibrido.Pendente,
                _ => throw new JsonException($"SituacaoHibrido desconhecida: {value}.")
            };
        }

        public override void Write(Utf8JsonWriter writer, SituacaoHibrido value, JsonSerializerOptions options)
        {
            string result = value switch
            {
                SituacaoHibrido.Criado => "A",
                SituacaoHibrido.NaoCriado => "N",
                SituacaoHibrido.Pendente => "P",
                _ => throw new JsonException($"SituacaoHibrido desconhecida: {value}.")
            };
            writer.WriteStringValue(result);
        }
    }
}
