namespace BoletoForBanrisul.Core.Validation
{
    /// <summary>
    /// Regular expressions and numeric bounds based on the bank's JSON Schema
    /// (<c>Docs/Ambiente-Cobrança-v1.6.0.postman_environment.json</c>, key
    /// <c>schemaRespostaRegistrarBoleto</c>) and subsequent published contract changes, used across the
    /// model's <see cref="System.ComponentModel.DataAnnotations.RegularExpressionAttribute"/>s.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every anchored pattern here ends in <c>\z</c>, not <c>$</c>. In .NET regex, <c>$</c> matches at the
    /// end of the string <b>or immediately before a trailing newline</b>, so <c>^\d{8}$</c> would accept
    /// <c>"90010260\n"</c>, a value the bank itself rejects. This is a deliberate deviation from how the
    /// same patterns are written in the Santander sibling library, which uses <c>$</c> throughout; it is not
    /// an oversight there being copied here.
    /// </para>
    /// <para>
    /// <see cref="Text"/> carries the character class only, with no length bound baked in. The bank repeats
    /// the identical character class across <c>pagador.nome</c> (40), <c>beneficiario.nome</c> (60),
    /// <c>mensagens[].texto</c> (75) and several other fields, each with its own <c>maxLength</c>. Writing
    /// the class once and pairing it with a per-property <c>[StringLength(n, MinimumLength = 1)]</c> keeps it
    /// from being transcribed (and drifting) five separate times, and it means a value with a valid
    /// character set but the wrong length produces exactly one error (the length one) instead of two,
    /// because <see cref="System.ComponentModel.DataAnnotations.RegularExpressionAttribute"/> and
    /// <see cref="System.ComponentModel.DataAnnotations.StringLengthAttribute"/> both fire independently.
    /// </para>
    /// <para>
    /// <see cref="BanrisulBounds.MoneyMin"/> is <c>"0"</c> even though the schema's own patterns
    /// (e.g. <c>^-?\d{1,12}\.\d{2}$</c>) allow a leading minus sign. That leading sign is <b>INFERRED</b> away:
    /// a negative bank slip amount, interest value, discount value or shared amount is not a meaningful
    /// request, and nothing in the collection's success scenarios ever sends one.
    /// </para>
    /// </remarks>
    public static class BanrisulPatterns
    {
        /// <summary>
        /// The character class shared by every free-text field in the schema (names, addresses, cities,
        /// message lines): letters (with the Portuguese accented set), digits, space and a fixed set of
        /// punctuation. Pair with <see cref="System.ComponentModel.DataAnnotations.StringLengthAttribute"/>
        /// for the length bound; see the type-level remarks.
        /// </summary>
        private const string TextClass =
            @"A-Za-z0-9áàâãéêíóôõúüçÁÀÂÃÉÊÍÓÔÕÚÜÇ \\!#$%'()*+,\-./:;=?@\[\]^_`{|}~&";

        /// <summary>Free text: names, addresses, cities, message lines. Pair with <c>[StringLength]</c> for the length.</summary>
        public const string Text = "^[" + TextClass + @"]+\z";

        /// <summary>Reference/identifier fields (<c>seu_numero</c>, <c>id_titulo_empresa</c>).</summary>
        public const string Reference = @"^[A-Za-z0-9 %$*+.,/\-&]+\z";

        /// <summary>1 to 10 digits. The response schema requires exactly 10 for <c>nosso_numero</c>, but the bank's own request examples send as few as 3 (e.g. <c>"264"</c>): the request is looser than the response.</summary>
        public const string DigitsUpTo10 = @"^\d{1,10}\z";

        /// <summary>Exactly 8 digits (<c>cep</c>).</summary>
        public const string Digits8 = @"^\d{8}\z";

        /// <summary>Exactly 13 digits (<c>beneficiario.codigo</c>, <c>rateio.beneficiarios[].codigo</c>).</summary>
        public const string Digits13 = @"^\d{13}\z";

        /// <summary>Exactly 44 digits (<c>notas_fiscais[].danfe</c>).</summary>
        public const string Digits44 = @"^\d{44}\z";

        /// <summary>Exactly 11 CPF digits, or 12 uppercase alphanumeric CNPJ characters followed by 2 check digits.</summary>
        public const string CpfOrCnpj = @"^(?:[0-9]{11}|[A-Z0-9]{12}[0-9]{2})\z";

        /// <summary>Exactly 2 uppercase letters (<c>uf</c>).</summary>
        public const string Uf = @"^[A-Z]{2}\z";
    }

    /// <summary>
    /// Numeric bounds for <see cref="DecimalRangeAttribute"/>, transcribed from the bank's JSON Schema
    /// decimal patterns. See <see cref="BanrisulPatterns"/>'s remarks for the <see cref="MoneyMin"/> note.
    /// </summary>
    public static class BanrisulBounds
    {
        /// <summary>INFERRED: a negative amount is meaningless, even though the schema pattern allows a leading <c>-</c>.</summary>
        public const string MoneyMin = "0";

        /// <summary>Matches <c>^-?\d{1,12}\.\d{2}$</c>: up to 12 integer digits, 2 decimal places.</summary>
        public const string MoneyMax = "999999999999.99";

        /// <summary>Matches <c>^-?\d{1,3}\.\d{2}$</c>: up to 3 integer digits, 2 decimal places (<c>juros.taxa</c>, <c>desconto.taxa</c>).</summary>
        public const string Rate3Max = "999.99";

        /// <summary>Matches <c>^-?\d{1,4}\.\d{1}$</c>: up to 4 integer digits, 1 decimal place (<c>multa.taxa</c>).</summary>
        public const string Rate4Max = "9999.9";

        /// <summary>Matches <c>^-?\d{1,3}\.\d{3}$</c>: up to 3 integer digits, 3 decimal places (<c>rateio.beneficiarios[].percentual</c>).</summary>
        public const string Percent3Max = "999.999";
    }
}
