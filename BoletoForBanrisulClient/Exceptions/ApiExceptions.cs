using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoletoForBanrisul.Client.Exceptions.Api
{
    public enum HttpErrorCode
    {
        Unknown = 0,

        // Client Error (4xx).
        BadRequest = 400,
        Unauthorized = 401,
        PaymentRequired = 402,
        Forbidden = 403,
        NotFound = 404,
        MethodNotAllowed = 405,
        NotAcceptable = 406,
        ProxyAuthenticationRequired = 407,
        RequestTimeout = 408,
        Conflict = 409,
        Gone = 410,
        LengthRequired = 411,
        PreconditionFailed = 412,
        PayloadTooLarge = 413,
        URITooLong = 414,
        UnsupportedMediaType = 415,
        RangeNotSatisfiable = 416,
        ExpectationFailed = 417,
        ImATeapot = 418,
        MisdirectedRequest = 421,
        UnprocessableEntity = 422,
        Locked = 423,
        FailedDependency = 424,
        TooEarly = 425,
        UpgradeRequired = 426,
        PreconditionRequired = 428,
        TooManyRequests = 429,
        RequestHeaderFieldsTooLarge = 431,
        UnavailableForLegalReasons = 451,

        // Server Error (5xx).
        InternalServerError = 500,
        NotImplemented = 501,
        BadGateway = 502,
        ServiceUnavailable = 503,
        GatewayTimeout = 504,
        HTTPVersionNotSupported = 505,
        VariantAlsoNegotiates = 506,
        InsufficientStorage = 507,
        LoopDetected = 508,
        NotExtended = 510,
        NetworkAuthenticationRequired = 511
    }

    public class ApiException : Exception
    {
        public HttpErrorCode HttpStatusCode { get; set; }
        public List<ApiOcurrence> Ocurrences { get; } = new List<ApiOcurrence>();

        public ApiException(string message, HttpErrorCode httpStatusCode) : base(message)
        {
            HttpStatusCode = httpStatusCode;
        }
    }

    public class InvalidRequestException : ApiException
    {
        public InvalidRequestException()
            : base("REQUISIÇÃO INVÁLIDA. O servidor não pôde processar a requisição devido a uma sintaxe de requisição malformada, estrutura da mensagem da requisição inválida, ou valores inválidos.", HttpErrorCode.BadRequest)
        {
        }
    }

    public class NotFoundException : ApiException
    {
        public NotFoundException()
            : base("NÃO ENCONTRADO. O servidor não conseguiu encontrar o recurso solicitado.", HttpErrorCode.NotFound)
        {
        }
    }

    public class UnprocessableEntityException : ApiException
    {
        public UnprocessableEntityException()
            : base("ENTIDADE NÃO PROCESSÁVEL. O servidor entende o tipo de conteúdo da entidade da requisição, e a sintaxe da requisição está correta, mas não foi possível processar as instruções presentes.", HttpErrorCode.UnprocessableEntity)
        {
        }
    }

    public class InternalServerErrorException : ApiException
    {
        public InternalServerErrorException()
            : base("ERRO INTERNO. O servidor encontrou uma condição inesperada que o impediu de atender a requisição.", HttpErrorCode.InternalServerError)
        {
        }
    }

    public class ServiceUnavailableException : ApiException
    {
        public ServiceUnavailableException()
            : base("SERVIÇO INDISPONÍVEL. O sistema encontra-se indisponível ou em atualização no momento, aguarde alguns minutos.", HttpErrorCode.ServiceUnavailable)
        {
        }
    }

    public class TooManyRequestsException : ApiException
    {
        public TooManyRequestsException()
            : base("MUITAS REQUISIÇÕES. O serviço recebeu um número excessivo de requisições em um curto período de tempo. Por favor, tente novamente mais tarde.", HttpErrorCode.TooManyRequests)
        {
        }
    }

    public class ResponseDeserializationException : ApiException
    {
        public ResponseDeserializationException(string details = "")
            : base($"ERRO DE DESSERIALIZAÇÃO. Ocorreu um erro ao desserializar a resposta do servidor. Detalhes: {details}", HttpErrorCode.Unknown)
        {
        }
    }

    public class ApiOcurrence
    {
        public string Code { get; }
        public string Message { get; }
        public string Complement { get; }

        public ApiOcurrence(string code, string message, string complement)
        {
            Code = code;
            Message = message;
            Complement = complement;
        }
    }
}
