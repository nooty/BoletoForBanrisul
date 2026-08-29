using System.Collections.Generic;

namespace BoletoForBanrisul.Client.HttpClients
{
    /// <summary>
    /// Clones an <see cref="HttpRequestMessage"/> from already buffered content. Used whenever a request
    /// has to be resent: an HttpRequestMessage cannot be sent more than once.
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
