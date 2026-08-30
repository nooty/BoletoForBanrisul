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
    /// <para>
    /// It implements <see cref="IDisposable"/> because <see cref="BoletoClientBuilder"/> always assembles
    /// and owns the two internal HttpClients (the API's and the authenticator's). Call
    /// <see cref="IDisposable.Dispose"/> (or use a using block) to release them once the client is no
    /// longer in use.
    /// </para>
    /// <para>
    /// Every operation reports a bank error as an <see cref="ApiException"/>, whose concrete type follows
    /// the response status:
    /// </para>
    /// <list type="table">
    ///   <item><term>400</term><description><see cref="InvalidRequestException"/></description></item>
    ///   <item><term>404</term><description><see cref="NotFoundException"/></description></item>
    ///   <item><term>422</term><description><see cref="UnprocessableEntityException"/></description></item>
    ///   <item><term>429</term><description><see cref="TooManyRequestsException"/></description></item>
    ///   <item><term>500</term><description><see cref="InternalServerErrorException"/></description></item>
    ///   <item><term>503</term><description><see cref="ServiceUnavailableException"/></description></item>
    ///   <item>
    ///     <term>any other status</term>
    ///     <description><see cref="ApiException"/> itself, carrying the code in <see cref="ApiException.HttpStatusCode"/></description>
    ///   </item>
    /// </list>
    /// <para>
    /// A success body that cannot be deserialised raises <see cref="ResponseDeserializationException"/>,
    /// also an <see cref="ApiException"/>. A 401 is the one exception to the rule: it raises
    /// <see cref="UnauthorizedException"/>, which derives from <see cref="AuthenticationException"/> and
    /// not from <see cref="ApiException"/>, so it never carries <see cref="ApiException.Occurrences"/>.
    /// </para>
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
        /// <exception cref="ApiException">
        /// The bank answered with an error status. The concrete type identifies which - see the mapping in
        /// this interface's remarks - and <see cref="ApiException.HttpStatusCode"/> and
        /// <see cref="ApiException.Occurrences"/> carry the detail.
        /// </exception>
        /// <exception cref="AuthenticationException">The OAuth token could not be obtained, or the bank rejected it.</exception>
        /// <exception cref="HttpRequestException">The request never reached the bank.</exception>
        /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
        Task<CreateBoletoResponse> CreateBoletoAsync(Boleto boleto, CancellationToken cancellationToken = default);

        /// <summary>
        /// Queries a boleto by its identifier (nosso número, barcode or linha digitável).
        /// </summary>
        /// <param name="idBoleto">The boleto's identifier.</param>
        /// <param name="typeId">Identifier type. Optional when idBoleto is the nosso número.</param>
        /// <param name="cancellationToken">Token that cancels the operation.</param>
        /// <returns>The boleto found.</returns>
        /// <exception cref="ApiException">
        /// The bank answered with an error status. The concrete type identifies which - see the mapping in
        /// this interface's remarks - and <see cref="ApiException.HttpStatusCode"/> and
        /// <see cref="ApiException.Occurrences"/> carry the detail.
        /// </exception>
        /// <exception cref="AuthenticationException">The OAuth token could not be obtained, or the bank rejected it.</exception>
        /// <exception cref="HttpRequestException">The request never reached the bank.</exception>
        /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
        Task<GetBoletoResponse> GetBoletoAsync(string idBoleto, BoletoIdentifierType? typeId = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Grants an abatement on a boleto.
        /// </summary>
        /// <param name="idBoleto">The boleto's identifier.</param>
        /// <param name="abatement">The abatement amount.</param>
        /// <param name="typeId">Optional when idBoleto is the nosso número.</param>
        /// <param name="cancellationToken">Token that cancels the operation.</param>
        /// <returns>The boleto with the abatement granted.</returns>
        /// <exception cref="ApiException">
        /// The bank answered with an error status. The concrete type identifies which - see the mapping in
        /// this interface's remarks - and <see cref="ApiException.HttpStatusCode"/> and
        /// <see cref="ApiException.Occurrences"/> carry the detail.
        /// </exception>
        /// <exception cref="AuthenticationException">The OAuth token could not be obtained, or the bank rejected it.</exception>
        /// <exception cref="HttpRequestException">The request never reached the bank.</exception>
        /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
        Task<GrantAbatementResponse> GrantAbatementAsync(string idBoleto, decimal abatement, BoletoIdentifierType? typeId = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Changes a boleto's due date.
        /// </summary>
        /// <param name="idBoleto">The boleto's identifier.</param>
        /// <param name="newDueDate">The new due date.</param>
        /// <param name="typeId">Optional when idBoleto is the nosso número.</param>
        /// <param name="cancellationToken">Token that cancels the operation.</param>
        /// <returns>The boleto with the new due date.</returns>
        /// <exception cref="ApiException">
        /// The bank answered with an error status. The concrete type identifies which - see the mapping in
        /// this interface's remarks - and <see cref="ApiException.HttpStatusCode"/> and
        /// <see cref="ApiException.Occurrences"/> carry the detail.
        /// </exception>
        /// <exception cref="AuthenticationException">The OAuth token could not be obtained, or the bank rejected it.</exception>
        /// <exception cref="HttpRequestException">The request never reached the bank.</exception>
        /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
        Task<ChangeDueDateResponse> ChangeDueDateAsync(string idBoleto, DateTime newDueDate, BoletoIdentifierType? typeId = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Writes off (cancels) a boleto.
        /// </summary>
        /// <param name="idBoleto">The boleto's identifier.</param>
        /// <param name="typeId">Optional when idBoleto is the nosso número.</param>
        /// <param name="cancellationToken">Token that cancels the operation.</param>
        /// <returns>The written-off (cancelled) boleto.</returns>
        /// <exception cref="ApiException">
        /// The bank answered with an error status. The concrete type identifies which - see the mapping in
        /// this interface's remarks - and <see cref="ApiException.HttpStatusCode"/> and
        /// <see cref="ApiException.Occurrences"/> carry the detail.
        /// </exception>
        /// <exception cref="AuthenticationException">The OAuth token could not be obtained, or the bank rejected it.</exception>
        /// <exception cref="HttpRequestException">The request never reached the bank.</exception>
        /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
        Task<CancelBoletoResponse> CancelBoletoAsync(string idBoleto, BoletoIdentifierType? typeId = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Issues a boleto as PDF.
        /// </summary>
        /// <param name="idBoleto">The boleto's identifier.</param>
        /// <param name="typeId">Optional when idBoleto is the nosso número.</param>
        /// <param name="cancellationToken">Token that cancels the operation.</param>
        /// <returns>The boleto issued as PDF.</returns>
        /// <exception cref="ApiException">
        /// The bank answered with an error status. The concrete type identifies which - see the mapping in
        /// this interface's remarks - and <see cref="ApiException.HttpStatusCode"/> and
        /// <see cref="ApiException.Occurrences"/> carry the detail.
        /// </exception>
        /// <exception cref="AuthenticationException">The OAuth token could not be obtained, or the bank rejected it.</exception>
        /// <exception cref="HttpRequestException">The request never reached the bank.</exception>
        /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
        Task<byte[]> IssueBoletoAsync(string idBoleto, BoletoIdentifierType? typeId = null, CancellationToken cancellationToken = default);
    }
}
