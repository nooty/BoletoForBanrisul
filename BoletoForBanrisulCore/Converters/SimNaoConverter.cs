using BoletoForBanrisul.Core.Models;
using BoletoForBanrisul.Core.Models.Types;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BoletoForBanrisul.Core.Converters
{
    public class SimNaoConverter : JsonConverter<SimNao>
    {
        public override SimNao Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            string value = reader.GetString() ?? "";
            return value switch
            {
                "S" => SimNao.Sim,
                "N" => SimNao.Nao,
                _ => throw new JsonException($"SimNao desconhecido: {value}.")
            };
        }

        public override void Write(Utf8JsonWriter writer, SimNao value, JsonSerializerOptions options)
        {
            string result = value switch
            {
                SimNao.Sim => "S",
                SimNao.Nao => "N",
                _ => throw new JsonException($"SimNao desconhecido: {value}.")
            };
            writer.WriteStringValue(result);
        }
    }
}
