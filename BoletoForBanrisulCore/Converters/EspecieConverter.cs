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

            // Enum.TryParse alone accepts any numeric string and returns true even when the resulting value
            // is not one of the enum's defined members (e.g. "77" parses to the undefined (Especie)77), so a
            // bad wire value would otherwise deserialize silently instead of failing loudly like every other
            // converter in this namespace. Enum.IsDefined closes that gap.
            if (Enum.TryParse<Especie>(value, out var result) && Enum.IsDefined(typeof(Especie), result))
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