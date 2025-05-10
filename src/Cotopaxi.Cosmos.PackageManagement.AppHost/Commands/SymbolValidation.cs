// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.Buffers;
using System.CommandLine;
using Cotopaxi.Cosmos.PackageManagement.AppHost.Invocation;

namespace Cotopaxi.Cosmos.PackageManagement.AppHost.Commands;

internal static class SymbolValidation
{
    private static readonly SearchValues<char> s_invalidPathChars = SearchValues.Create(Path.GetInvalidPathChars());

    public static void AddValidationAsOutputFile(this Argument<string> argument)
    {
        argument.AddValidator(
            static x => !x.AsSpan().ContainsAny(s_invalidPathChars),
            static x => $"The file path '{x}' is invalid");
    }

    public static void AddValidationAsInputFile(this Argument<string> argument)
    {
        argument.AddValidator(
            static x => File.Exists(x),
            static x => $"The file '{x}' could not be found");
    }

    public static void AddValidationAsOutputFile(this Option<string> option)
    {
        option.AddValidator(
            static x => !x.AsSpan().ContainsAny(s_invalidPathChars),
            static x => $"The file path '{x}' is invalid");
    }

    public static void AddValidationAsHttpsUri(this Option<Uri> option)
    {
        option.AddValidator(
            static x => x is { IsAbsoluteUri: true } && x.Scheme == Uri.UriSchemeHttps,
            static x => $"The value '{x}' is not an HTTPS URI");
    }
}
