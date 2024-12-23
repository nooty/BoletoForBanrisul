using BoletoForBanrisul.Client.Converters;
using BoletoForBanrisul.Core.Converters;
using BoletoForBanrisul.Core.Models;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace BoletoForBanrisul.Client.Models.Responses
{
    public class BaseResponse
    {
        [JsonConverter(typeof(ReturnCodeConverter))]
        public ResponseCode Retorno { get; set; }
    }

    public class CreateBoletoResponse : BaseResponse
    {
        public Titulo? Titulo { get; set; }
    }

    public class GetBoletoResponse : BaseResponse
    {
        public Titulo? Titulo { get; set; }
    }

    public class ChangeDueDateResponse : BaseResponse
    {
        public ChangeDueDateContent? Titulo { get; set; }
    }

    public class GrantAbatementResponse : BaseResponse
    {
        public GrantAbatementContent? Titulo { get; set; }
    }

    public class CancelBoletoResponse : BaseResponse
    {
        public CancelBoletoContent? Titulo { get; set; }
    }

    public class IssueBoletoResponse : BaseResponse
    {
        public IssueBoletoContent Titulo { get; set; } = new IssueBoletoContent();
    }

    public class IssueBoletoContent
    {
        public string Boleto { get; set; } = "";
    }

    public class ChangeDueDateContent
    {
        public string NossoNumero { get; set; } = "";

        [JsonConverter(typeof(DateOnlyConverter))]
        public DateTime DataVencimento { get; set; }

        public string CodigoBarra { get; set; } = "";

        public string LinhaDigitavel { get; set; } = "";

        public BeneficiaryResponse Beneficiario { get; set; } = new BeneficiaryResponse();
    }

    public class GrantAbatementContent
    {
        public string NossoNumero { get; set; } = "";

        public string CodigoBarra { get; set; } = "";

        public string LinhaDigitavel { get; set; } = "";

        public BeneficiaryResponse Beneficiario { get; set; } = new BeneficiaryResponse();
    }

    public class CancelBoletoContent
    {
        public string NossoNumero { get; set; } = "";

        public string CodigoBarra { get; set; } = "";

        public string LinhaDigitavel { get; set; } = "";

        public BeneficiaryResponse Beneficiario { get; set; } = new BeneficiaryResponse();
    }

    public class BeneficiaryResponse
    {
        public string Codigo { get; set; } = "";
    }

    public class ErrorResponse
    {
        public List<ErrorOccurrence> Ocorrencias { get; set; } = new List<ErrorOccurrence>();    
    }

    public class ErrorOccurrence
    {
        public string Codigo { get; set; } = "";
        public string Mensagem { get; set; } = "";
        public string Complemento { get; set; } = "";
    }

    public enum ResponseCode
    {
        Success = 1,
        SuccessAndCentralized = 2,
        Failure = 3,
        TestApproved = 4
    }
}
