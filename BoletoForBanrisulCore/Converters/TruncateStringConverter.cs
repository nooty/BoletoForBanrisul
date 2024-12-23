using BoletoForBanrisul.Core.Models;
using BoletoForBanrisul.Core.Models.Types;
using System;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BoletoForBanrisul.Core.Converters
{
    [AttributeUsage(AttributeTargets.Property)]
    public class TruncateStringAttribute : JsonConverterAttribute
    {
        private readonly int _length;

        public TruncateStringAttribute(int length) => _length = length;

        public override JsonConverter CreateConverter(Type typeToConvert)
        {
            return new TruncateStringConverter(_length);
        }
    }

    public class TruncateStringConverter : JsonConverter<string?>
    {
        private readonly int _maxLength;

        public TruncateStringConverter(int maxLength)
        {
            _maxLength = maxLength;
        }

        public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return reader.GetString();
        }

        public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
        {
            if(value == null)
            {
                writer.WriteNullValue();
                return;
            }

            string truncatedValue = value.Length > _maxLength ? value.Substring(0, _maxLength) : value;

            writer.WriteStringValue(truncatedValue);
        }
    }
}