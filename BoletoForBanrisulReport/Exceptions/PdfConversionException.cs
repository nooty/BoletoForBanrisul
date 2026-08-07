using System;

namespace BoletoForBanrisul.Report.Exceptions
{
    /// <summary>
    /// Falha na conversão de HTML para PDF. A causa original vem em <see cref="Exception.InnerException"/>.
    /// </summary>
    /// <remarks>
    /// É o tipo a retentar, em vez de um <c>Handle&lt;Exception&gt;</c> que pegaria também
    /// cancelamento e disco cheio - casos em que retentar só aumenta o tempo preso. Por isso não
    /// envolve <see cref="OperationCanceledException"/>, <see cref="TimeoutException"/> nem
    /// <see cref="InsufficientDiskSpaceException"/>, que saem com o tipo original.
    /// </remarks>
    public class PdfConversionException : Exception
    {
        /// <summary>
        /// Cria a exceção com a mensagem informada.
        /// </summary>
        /// <param name="message">Descrição da falha.</param>
        public PdfConversionException(string message) : base(message)
        {
        }

        /// <summary>
        /// Cria a exceção com a mensagem informada e a causa original.
        /// </summary>
        /// <param name="message">Descrição da falha.</param>
        /// <param name="innerException">A exceção que originou a falha.</param>
        public PdfConversionException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
