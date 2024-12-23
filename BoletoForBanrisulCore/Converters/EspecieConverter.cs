using BoletoForBanrisul.Core.Models;
using BoletoForBanrisul.Core.Models.Types;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BoletoForBanrisul.Core.Converters
{
    public class EspecieConverter : JsonConverter<Especie>
    {
        public override Especie Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            string value = reader.GetString() ?? "";
            if (Enum.TryParse<Especie>(value, out var result))
            {
                return result;
            }
            throw new JsonException($"Invalid value for Especie: {value}.");
        }

        public override void Write(Utf8JsonWriter writer, Especie value, JsonSerializerOptions options)
        {
            // Obtém o valor numérico do enum e o converte para uma string com 2 dígitos
            string formattedValue = ((int)value).ToString("D2");
            writer.WriteStringValue(formattedValue);
        }
    }
}