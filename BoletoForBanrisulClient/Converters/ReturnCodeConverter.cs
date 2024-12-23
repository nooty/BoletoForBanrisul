using BoletoForBanrisul.Client.Models.Responses;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BoletoForBanrisul.Client.Converters
{
    public class ReturnCodeConverter : JsonConverter<ResponseCode>
    {
        public override ResponseCode Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var str = reader.GetString();
            if (int.TryParse(str, out int intValue))
            {
                return (ResponseCode)intValue;
            }

            throw new JsonException($"CodigoRetorno desconhecido: {reader.GetString()}.");
        }

        public override void Write(Utf8JsonWriter writer, ResponseCode value, JsonSerializerOptions options)
        {
            var intVal = (int)value;

            writer.WriteStringValue((intVal + "").PadLeft(2, '0'));
        }
    }
}
