using System.Collections.Generic;

namespace BoletoForBanrisul.Client.HttpClients
{
    /// <summary>
    /// Clona uma <see cref="HttpRequestMessage"/> a partir do conteúdo já bufferizado. Usado sempre que uma
    /// requisição precisa ser reenviada: uma HttpRequestMessage não pode ser enviada mais de uma vez.
    /// </summary>
    internal static class HttpRequestCloner
    {
        internal static HttpRequestMessage Clone(HttpRequestMessage request, byte[]? contentBytes, List<KeyValuePair<string, IEnumerable<string>>>? contentHeaders)
        {
            var clone = new HttpRequestMessage(request.Method, request.RequestUri)
            {
                Version = request.Version
            };

            foreach (var header in request.Headers)
            {
                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            foreach (var property in request.Properties)
            {
                clone.Properties.Add(property.Key, property.Value);
            }

            if (contentBytes != null)
            {
                var content = new ByteArrayContent(contentBytes);

                if (contentHeaders != null)
                {
                    foreach (var header in contentHeaders)
                    {
                        content.Headers.TryAddWithoutValidation(header.Key, header.Value);
                    }
                }

                clone.Content = content;
            }

            return clone;
        }
    }
}
