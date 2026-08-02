using BoletoForBanrisul.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BoletoForBanrisul.Client.Exceptions.Api;
using BoletoForBanrisul.Client.Exceptions.Auth;
using BoletoForBanrisul.Client.Models.Responses;

namespace BoletoForBanrisul.Client
{
    /// <summary>
    /// Cliente para a API de cobrança do Banrisul.
    /// </summary>
    /// <remarks>
    /// Implementa <see cref="IDisposable"/> porque <see cref="BoletoClientBuilder"/> sempre monta e é
    /// proprietário dos dois HttpClient internos (o da API e o do autenticador). Chame
    /// <see cref="IDisposable.Dispose"/> (ou utilize um bloco using) para liberá-los quando o client não for
    /// mais utilizado.
    /// </remarks>
    public interface IBoletoClient : IDisposable
    {
        /// <summary>
        /// Registra um boleto no banco.
        /// </summary>
        /// <param name="boleto">
        /// Objeto boleto a ser registrado. Validado localmente por <see cref="BoletoForBanrisul.Core.Validation.RecursiveValidator"/>
        /// antes de qualquer requisição HTTP ser enviada.
        /// </param>
        /// <param name="cancellationToken">Token para cancelar a operação.</param>
        /// <returns>Boleto registrado.</returns>
        /// <exception cref="BoletoForBanrisul.Core.Validation.ModelValidationException">The bank slip failed local validation; no HTTP request was made.</exception>
        /// <exception cref="InvalidRequestException"/>
        /// <exception cref="UnauthorizedException"/>
        /// <exception cref="TokenDeserializationException"/>
        /// <exception cref="AuthenticationException"/>
        /// <exception cref="NotFoundException"/>
        /// <exception cref="InternalServerErrorException"/>
        /// <exception cref="TooManyRequestsException"/>
        /// <exception cref="ServiceUnavailableException"/>
        /// <exception cref="ResponseDeserializationException"/>
        /// <exception cref="HttpRequestException"/>
        Task<CreateBoletoResponse> CreateBoletoAsync(Boleto boleto, CancellationToken cancellationToken = default);

        /// <summary>
        /// Consulta um boleto pelo identificador (nosso número, código de barras ou linha digitável).
        /// </summary>
        /// <param name="idBoleto">Identificador do boleto.</param>
        /// <param name="typeId">Tipo de identificador. Opcional se o idBoleto for o nosso número.</param>
        /// <param name="cancellationToken">Token para cancelar a operação.</param>
        /// <returns>Boleto consultado.</returns>
        /// <exception cref="InvalidRequestException"/>
        /// <exception cref="UnauthorizedException"/>
        /// <exception cref="TokenDeserializationException"/>
        /// <exception cref="AuthenticationException"/>
        /// <exception cref="NotFoundException"/>
        /// <exception cref="InternalServerErrorException"/>
        /// <exception cref="TooManyRequestsException"/>
        /// <exception cref="ServiceUnavailableException"/>
        /// <exception cref="ResponseDeserializationException"/>
        /// <exception cref="HttpRequestException"/>
        Task<GetBoletoResponse> GetBoletoAsync(string idBoleto, BoletoIdentifierType? typeId = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Concede abatimento em um boleto.
        /// </summary>
        /// <param name="idBoleto">Identificador do boleto.</param>
        /// <param name="abatement">Valor do abatimento.</param>
        /// <param name="typeId">Opcional se idBoleto for o nosso número.</param>
        /// <param name="cancellationToken">Token para cancelar a operação.</param>
        /// <returns>Boleto com abatimento concedido.</returns>
        /// <exception cref="InvalidRequestException"/>
        /// <exception cref="UnauthorizedException"/>
        /// <exception cref="TokenDeserializationException"/>
        /// <exception cref="AuthenticationException"/>
        /// <exception cref="NotFoundException"/>
        /// <exception cref="InternalServerErrorException"/>
        /// <exception cref="TooManyRequestsException"/>
        /// <exception cref="ServiceUnavailableException"/>
        /// <exception cref="ResponseDeserializationException"/>
        /// <exception cref="HttpRequestException"/>
        Task<GrantAbatementResponse> GrantAbatementAsync(string idBoleto, decimal abatement, BoletoIdentifierType? typeId = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Altera a data de vencimento de um boleto.
        /// </summary>
        /// <param name="idBoleto">Identificador do boleto.</param>
        /// <param name="newDueDate">Nova data de vencimento.</param>
        /// <param name="typeId">Opcional se idBoleto for o nosso número.</param>
        /// <param name="cancellationToken">Token para cancelar a operação.</param>
        /// <returns>Boleto com a nova data de vencimento.</returns>
        /// <exception cref="InvalidRequestException"/>
        /// <exception cref="UnauthorizedException"/>
        /// <exception cref="TokenDeserializationException"/>
        /// <exception cref="AuthenticationException"/>
        /// <exception cref="NotFoundException"/>
        /// <exception cref="InternalServerErrorException"/>
        /// <exception cref="TooManyRequestsException"/>
        /// <exception cref="ServiceUnavailableException"/>
        /// <exception cref="ResponseDeserializationException"/>
        /// <exception cref="HttpRequestException"/>
        Task<ChangeDueDateResponse> ChangeDueDateAsync(string idBoleto, DateTime newDueDate, BoletoIdentifierType? typeId = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Baixa (cancela) um boleto.
        /// </summary>
        /// <param name="idBoleto">Identificador do boleto.</param>
        /// <param name="typeId">Opcional se idBoleto for o nosso número.</param>
        /// <param name="cancellationToken">Token para cancelar a operação.</param>
        /// <returns>Boleto baixado (cancelado).</returns>
        /// <exception cref="InvalidRequestException"/>
        /// <exception cref="UnauthorizedException"/>
        /// <exception cref="TokenDeserializationException"/>
        /// <exception cref="AuthenticationException"/>
        /// <exception cref="NotFoundException"/>
        /// <exception cref="InternalServerErrorException"/>
        /// <exception cref="TooManyRequestsException"/>
        /// <exception cref="ServiceUnavailableException"/>
        /// <exception cref="ResponseDeserializationException"/>
        /// <exception cref="HttpRequestException"/>
        Task<CancelBoletoResponse> CancelBoletoAsync(string idBoleto, BoletoIdentifierType? typeId = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Emite um boleto em PDF.
        /// </summary>
        /// <param name="idBoleto">Identificador do boleto.</param>
        /// <param name="typeId">Opcional se idBoleto for o nosso número.</param>
        /// <param name="cancellationToken">Token para cancelar a operação.</param>
        /// <returns>Boleto emitido em PDF.</returns>
        /// <exception cref="InvalidRequestException"/>
        /// <exception cref="UnauthorizedException"/>
        /// <exception cref="TokenDeserializationException"/>
        /// <exception cref="AuthenticationException"/>
        /// <exception cref="NotFoundException"/>
        /// <exception cref="InternalServerErrorException"/>
        /// <exception cref="TooManyRequestsException"/>
        /// <exception cref="ServiceUnavailableException"/>
        /// <exception cref="ResponseDeserializationException"/>
        /// <exception cref="HttpRequestException"/>
        Task<byte[]> IssueBoletoAsync(string idBoleto, BoletoIdentifierType? typeId = null, CancellationToken cancellationToken = default);
    }
}
