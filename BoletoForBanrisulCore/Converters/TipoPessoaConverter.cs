using BoletoForBanrisul.Core.Models;
using BoletoForBanrisul.Core.Models.Types;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BoletoForBanrisul.Core.Converters
{
    public class TipoPessoaConverter : JsonConverter<TipoPessoa>
    {
        public override TipoPessoa Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            string value = reader.GetString() ?? "";
            return value switch
            {
                "F" => TipoPessoa.PessoaFisica,
                "J" => TipoPessoa.PessoaJuridica,
                _ => throw new JsonException($"TipoPessoa desconhecido: {value}.")
            };
        }

        public override void Write(Utf8JsonWriter writer, TipoPessoa value, JsonSerializerOptions options)
        {
            string tipo = value switch
            {
                TipoPessoa.PessoaFisica => "F",
                TipoPessoa.PessoaJuridica => "J",
                _ => throw new JsonException($"TipoPessoa desconhecido: {value}.")
            };
            writer.WriteStringValue(tipo);
        }
    }
}
