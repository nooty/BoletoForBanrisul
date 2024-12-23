using BoletoForBanrisul.Core.Models;
using BoletoForBanrisul.Core.Models.Types;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BoletoForBanrisul.Core.Converters
{
    public class AceiteConverter : JsonConverter<Aceite>
    {
        public override Aceite Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            string value = reader.GetString() ?? "";
            return value switch
            {
                "A" => Aceite.Sim,
                "N" => Aceite.Nao,
                _ => throw new JsonException($"Aceite desconhecido: {value}.")
            };
        }

        public override void Write(Utf8JsonWriter writer, Aceite value, JsonSerializerOptions options)
        {
            string result = value switch
            {
                Aceite.Sim => "A",
                Aceite.Nao => "N",
                _ => throw new JsonException($"Aceite desconhecido: {value}.")
            };
            writer.WriteStringValue(result);
        }
    }
}
