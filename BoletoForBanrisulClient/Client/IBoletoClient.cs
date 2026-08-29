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
    /// Client for Banrisul's billing API.
    /// </summary>
    /// <remarks>
    /// It implements <see cref="IDisposable"/> because <see cref="BoletoClientBuilder"/> always assembles
    /// and owns the two internal HttpClients (the API's and the authenticator's). Call
    /// <see cref="IDisposable.Dispose"/> (or use a using block) to release them once the client is no
    /// longer in use.
    /// </remarks>
    public interface IBoletoClient : IDisposable
    {
        /// <summary>
        /// Registers a boleto with the bank.
        /// </summary>
        /// <param name="boleto">
        /// The boleto to register. Validated locally by <see cref="BoletoForBanrisul.Core.Validation.RecursiveValidator"/>
        /// before any HTTP request is sent.
        /// </param>
        /// <param name="cancellationToken">Token that cancels the operation.</param>
        /// <returns>The registered boleto.</returns>
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
        /// Queries a boleto by its identifier (nosso número, barcode or linha digitável).
        /// </summary>
        /// <param name="idBoleto">The boleto's identifier.</param>
        /// <param name="typeId">Identifier type. Optional when idBoleto is the nosso número.</param>
        /// <param name="cancellationToken">Token that cancels the operation.</param>
        /// <returns>The boleto found.</returns>
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
        /// Grants an abatement on a boleto.
        /// </summary>
        /// <param name="idBoleto">The boleto's identifier.</param>
        /// <param name="abatement">The abatement amount.</param>
        /// <param name="typeId">Optional when idBoleto is the nosso número.</param>
        /// <param name="cancellationToken">Token that cancels the operation.</param>
        /// <returns>The boleto with the abatement granted.</returns>
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
        /// Changes a boleto's due date.
        /// </summary>
        /// <param name="idBoleto">The boleto's identifier.</param>
        /// <param name="newDueDate">The new due date.</param>
        /// <param name="typeId">Optional when idBoleto is the nosso número.</param>
        /// <param name="cancellationToken">Token that cancels the operation.</param>
        /// <returns>The boleto with the new due date.</returns>
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
        /// Writes off (cancels) a boleto.
        /// </summary>
        /// <param name="idBoleto">The boleto's identifier.</param>
        /// <param name="typeId">Optional when idBoleto is the nosso número.</param>
        /// <param name="cancellationToken">Token that cancels the operation.</param>
        /// <returns>The written-off (cancelled) boleto.</returns>
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
        /// Issues a boleto as PDF.
        /// </summary>
        /// <param name="idBoleto">The boleto's identifier.</param>
        /// <param name="typeId">Optional when idBoleto is the nosso número.</param>
        /// <param name="cancellationToken">Token that cancels the operation.</param>
        /// <returns>The boleto issued as PDF.</returns>
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
