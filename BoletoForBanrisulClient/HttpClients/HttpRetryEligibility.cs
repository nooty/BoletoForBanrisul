namespace BoletoForBanrisul.Client.HttpClients
{
    /// <summary>
    /// Decides which requests <see cref="PolicyHandler"/> may resend.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Eligibility is per request, not per client, because the cost of an undue resend varies by
    /// operation. The default resends idempotent verbs only: a <c>POST</c> resent after the response
    /// was lost on the way back can be processed twice by the bank, and registering a boleto has no
    /// documented idempotency guarantee - the result would be a second boleto out of a single call
    /// by the library's user.
    /// </para>
    /// <para>
    /// Non-idempotent requests declare themselves with <see cref="MarkRetrySafe"/>, one by one. The
    /// mark is an assertion by whoever builds the request, not a deduction by the handler; that is
    /// why the default is the safe side, and a new endpoint nobody analysed is born without retry
    /// instead of inheriting it silently. Today the only mark is the OAuth token exchange: none of
    /// the billing operations is resent, because Banrisul's API documents idempotency for none.
    /// </para>
    /// </remarks>
    internal static class HttpRetryEligibility
    {
        // Properties, not Options: HttpRequestMessage.Options only exists from .NET 5 onwards, and
        // this library targets netstandard2.0.
        private const string RetrySafeProperty = "BoletoForBanrisul.Client.RetrySafe";

        /// <summary>
        /// Declares this request safe to resend, even when the verb is not idempotent. Use it only
        /// when repeating the call provably leads to the same final state - looking harmless is not
        /// enough; when in doubt about what the server does with a second send, do not mark it.
        /// </summary>
        /// <remarks>
        /// The mark travels in <see cref="HttpRequestMessage.Properties"/>, which
        /// <see cref="HttpRequestCloner"/> copies, so it survives the clone made for each attempt.
        /// </remarks>
        internal static void MarkRetrySafe(this HttpRequestMessage request)
        {
            request.Properties[RetrySafeProperty] = true;
        }

        /// <summary>
        /// Tells whether the request may be resent: either the verb is idempotent, or it was marked
        /// explicitly with <see cref="MarkRetrySafe"/>.
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
