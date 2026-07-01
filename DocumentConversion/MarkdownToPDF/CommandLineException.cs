using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Datalogics.PDFL;

namespace MarkdownToPdf;

internal sealed class CommandLineException : Exception
{
    public CommandLineException(string message) : base(message)
    {
    }

    public CommandLineException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
