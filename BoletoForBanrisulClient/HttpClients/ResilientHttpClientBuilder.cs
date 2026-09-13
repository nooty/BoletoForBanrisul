using Polly.Timeout;
using Polly;
using System;
using System.Collections.Generic;
using System.Text;
using Polly.Extensions.Http;
using Polly.Contrib.WaitAndRetry;
using System.Net.Http;

namespace BoletoForBanrisul.Client.HttpClients
{
    public class ResilientHttpClientBuilder
    {
        private HttpMessageHandler? _httpClientHandler;
        private TimeSpan _timeout = TimeSpan.FromSeconds(100);
        private string? _baseAddress;
        private Action<HttpClient>? _configureClientAction;

        public ResilientHttpClientBuilder()
        {
        }

        public ResilientHttpClientBuilder SetTimeout(TimeSpan timeout)
        {
            _timeout = timeout;
            return this;
        }

        public ResilientHttpClientBuilder SetBaseAddress(string baseAddress)
        {
            _baseAddress = baseAddress;
            return this;
        }

        /// <summary>
        /// Sets the handler the built HttpClient sends through. The builder never takes ownership of it:
        /// disposing the HttpClient leaves the handler alive.
        /// </summary>
        public ResilientHttpClientBuilder SetHandler(HttpMessageHandler handler)
        {
            _httpClientHandler = handler;
            return this;
        }

        public ResilientHttpClientBuilder Configure(Action<HttpClient> configureClient)
        {
            _configureClientAction = configureClient;
            return this;
        }

        public HttpClient Build()
        {
            // Decorrelated Jitter Backoff V2 strategy.
            var retryDelays = Backoff.DecorrelatedJitterBackoffV2(TimeSpan.FromSeconds(1), retryCount: 3);

            // Retry and timeout policies, through Polly.
            var retryPolicy = HttpPolicyExtensions
                .HandleTransientHttpError()
                .WaitAndRetryAsync(retryDelays);

            // Falls back to the shared transport when no custom handler was supplied.
            _httpClientHandler ??= DefaultTransportHandler.Instance;

            // The HttpClient never owns the chain: the transport is shared across clients and the
            // DelegatingHandlers above it hold no unmanaged resources.
            var httpClient = new HttpClient(new PolicyHandler(retryPolicy, _httpClientHandler), disposeHandler: false)
            {
                Timeout = _timeout
            };

            // Sets the base address, when supplied.
            if (!string.IsNullOrWhiteSpace(_baseAddress))
            {
                httpClient.BaseAddress = new Uri(_baseAddress);
            }

            // Hook for further HttpClient configuration.
            _configureClientAction?.Invoke(httpClient);

            return httpClient;
        }
    }
}
