namespace BoletoForBanrisul.Client.HttpClients
{
    /// <summary>
    /// Decide quais requisições o <see cref="PolicyHandler"/> pode reenviar.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A elegibilidade é por requisição, não por cliente, porque o custo de um reenvio indevido varia
    /// por operação. O padrão só reenvia verbos idempotentes: um <c>POST</c> reenviado depois de a
    /// resposta se perder na volta pode ser processado duas vezes pelo banco, e o registro de boleto
    /// não tem garantia de idempotência documentada - o resultado seria um segundo boleto a partir de
    /// uma única chamada de quem usa a lib.
    /// </para>
    /// <para>
    /// Requisições não idempotentes que sabidamente convergem para o mesmo estado quando repetidas se
    /// declaram com <see cref="MarkRetrySafe"/>. A marcação é uma afirmação de quem monta a
    /// requisição, não uma dedução do handler; por isso o padrão é o lado seguro, e um endpoint novo
    /// que ninguém analisou nasce sem retry em vez de herdá-lo em silêncio.
    /// </para>
    /// </remarks>
    internal static class HttpRetryEligibility
    {
        // Properties, e não Options: HttpRequestMessage.Options só existe a partir do .NET 5, e esta
        // biblioteca tem netstandard2.0 como alvo.
        private const string RetrySafeProperty = "BoletoForBanrisul.Client.RetrySafe";

        /// <summary>
        /// Declara que esta requisição pode ser reenviada com segurança, mesmo que o verbo não seja
        /// idempotente. Use apenas quando repetir a chamada leva ao mesmo estado final - nunca em uma
        /// operação que cria um recurso novo a cada envio.
        /// </summary>
        /// <remarks>
        /// A marca viaja em <see cref="HttpRequestMessage.Properties"/>, que
        /// <see cref="HttpRequestCloner"/> copia, então sobrevive aos clones feitos a cada tentativa.
        /// </remarks>
        internal static void MarkRetrySafe(this HttpRequestMessage request)
        {
            request.Properties[RetrySafeProperty] = true;
        }

        /// <summary>
        /// Indica se a requisição pode ser reenviada: ou o verbo é idempotente, ou ela foi marcada
        /// explicitamente com <see cref="MarkRetrySafe"/>.
        /// </summary>
        internal static bool IsRetrySafe(HttpRequestMessage request)
        {
            return IsIdempotentMethod(request.Method) || request.Properties.ContainsKey(RetrySafeProperty);
        }

        private static bool IsIdempotentMethod(HttpMethod method)
        {
            return method == HttpMethod.Get
                || method == HttpMethod.Head
                || method == HttpMethod.Options
                || method == HttpMethod.Trace
                || method == HttpMethod.Put
                || method == HttpMethod.Delete;
        }
    }
}
