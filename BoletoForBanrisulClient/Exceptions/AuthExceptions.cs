using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoletoForBanrisul.Client.Exceptions.Auth
{
    public class AuthenticationException : Exception
    {
        public AuthenticationException(string message) : base(message)
        {
        }
    }

    public class UnauthorizedException : AuthenticationException
    {
        public UnauthorizedException(string message = "NÃO AUTORIZADO. O servidor não conseguiu autenticar o usuário.") : base(message)
        {
        }
    }

    public class TokenDeserializationException : AuthenticationException
    {
        public TokenDeserializationException(string message = "ERRO DE DESSERIALIZAÇÃO. Erro ao desserializar o token durante a autenticação.") : base(message)
        {
        }
    }
}
