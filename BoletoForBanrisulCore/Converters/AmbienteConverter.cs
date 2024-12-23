using BoletoForBanrisul.Core.Models;
using BoletoForBanrisul.Core.Models.Types;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BoletoForBanrisul.Core.Converters
{
    public class AmbienteConverter : JsonConverter<Ambiente>
    {
        public override Ambiente Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            string value = reader.GetString() ?? "";
            return value switch
            {
                "P" => Ambiente.Producao,
                "T" => Ambiente.Teste,
                _ => throw new JsonException($"Ambiente desconhecido: {value}.")
            };
        }

        public override void Write(Utf8JsonWriter writer, Ambiente value, JsonSerializerOptions options)
        {
            string ambiente = value switch
            {
                Ambiente.Producao => "P",
                Ambiente.Teste => "T",
                _ => throw new JsonException($"Ambiente desconhecido: {value}.")
            };
            writer.WriteStringValue(ambiente);
        }
    }
}
