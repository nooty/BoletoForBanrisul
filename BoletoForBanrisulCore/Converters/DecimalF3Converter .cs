using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BoletoForBanrisul.Core.Converters
{
    public class DecimalF3Converter : JsonConverter<decimal>
    {
        private const string DecimalFormat = "F3";

        public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            string value = reader.GetString() ?? "";
            if (decimal.TryParse(value, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out decimal result))
            {
                return result;
            }

            throw new JsonException($"Invalid decimal value: {value}.");
        }

        public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options)
        {
            // Formata o valor decimal como string com duas casas decimais
            writer.WriteStringValue(value.ToString(DecimalFormat, CultureInfo.InvariantCulture));
        }
    }
}
