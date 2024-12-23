using BoletoForBanrisul.Core.Converters;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BoletoForBanrisul.Client.Models.Operations
{
    internal class ChangeDueDateRequest
    {
        public ChangeDueDateDetails? Titulo { get; set; } 
    }

    internal class ChangeDueDateDetails
    {
        [JsonConverter(typeof(DateOnlyConverter))]
        public DateTime? DataVencimento { get; set; }
    }

    internal class GrantAbatementRequest
    {
        public GrantAbatementDetails? Titulo { get; set; }
    }

    internal class GrantAbatementDetails
    {

        public GrantAbatementInstructions? Instrucoes { get; set; }
    }

    internal class GrantAbatementInstructions
    {
        public GrantAbatementValue? Abatimento { get; set; }  
    }

    internal class GrantAbatementValue
    {
        [JsonConverter(typeof(DecimalF2Converter))]
        public decimal Valor { get; set; }
    }

    internal static class OperationExtensions
    {
        public static string ToJson(this ChangeDueDateRequest operacao)
        {
            return JsonSerializer.Serialize(operacao, new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                PropertyNamingPolicy = new SnakeCaseLowerNamingPolicy()
            });
        }

        public static string ToJson(this GrantAbatementRequest operacao)
        {
            return JsonSerializer.Serialize(operacao, new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                PropertyNamingPolicy = new SnakeCaseLowerNamingPolicy()
            });
        }
    }
}
