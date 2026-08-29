using System;

namespace BoletoForBanrisul.Report.Exceptions
{
    /// <summary>
    /// HTML to PDF conversion failed. The original cause comes in <see cref="Exception.InnerException"/>.
    /// </summary>
    /// <remarks>
    /// This is the type to retry on, rather than a <c>Handle&lt;Exception&gt;</c> that would also catch
    /// cancellation and a full disk - cases where retrying only lengthens the time spent stuck. That is
    /// why it wraps neither <see cref="OperationCanceledException"/>, <see cref="TimeoutException"/> nor
    /// <see cref="InsufficientDiskSpaceException"/>, which surface with their original type.
    /// </remarks>
    public class PdfConversionException : Exception
    {
        /// <summary>
        /// Creates the exception with the given message.
        /// </summary>
        /// <param name="message">Description of the failure.</param>
        public PdfConversionException(string message) : base(message)
        {
        }

        /// <summary>
        /// Creates the exception with the given message and the original cause.
        /// </summary>
        /// <param name="message">Description of the failure.</param>
        /// <param name="innerException">The exception that caused the failure.</param>
        public PdfConversionException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
