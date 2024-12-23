using System;
namespace Test
{
    public abstract class AuthenticatedTestBase
    {
        protected string ClientId { get; private set; }
        protected string ClientSecret { get; private set; }

        public AuthenticatedTestBase()
        {
            ClientId = GetEnvironmentVariable("CLIENT_ID");
            ClientSecret = GetEnvironmentVariable("CLIENT_SECRET");

            if (string.IsNullOrEmpty(ClientId) || string.IsNullOrEmpty(ClientSecret))
            {
                throw new InvalidOperationException("As variáveis de ambiente CLIENT_ID e CLIENT_SECRET devem estar definidas.");
            }
        }

        private static string GetEnvironmentVariable(string name)
        {
            return Environment.GetEnvironmentVariable(name) ??
                   Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User) ??
                   "";
        }
    }
}
