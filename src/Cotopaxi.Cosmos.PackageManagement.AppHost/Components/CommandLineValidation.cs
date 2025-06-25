// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.Buffers;
using System.CommandLine;
using System.CommandLine.Parsing;

namespace Cotopaxi.Cosmos.PackageManagement.AppHost.Components;

internal static class CommandLineValidation
{
    private static readonly SearchValues<char> s_invalidPathChars = SearchValues.Create(Path.GetInvalidPathChars());

    public static Argument<string> AsOutputFile(this Argument<string> argument)
    {
        return AddValidator(
            argument,
            static x => !x.AsSpan().ContainsAny(s_invalidPathChars),
            static x => $"The file path '{x}' is invalid");
    }

    public static Argument<string> AsInputFile(this Argument<string> argument)
    {
        return AddValidator(
            argument,
            static x => File.Exists(x),
            static x => $"The file '{x}' could not be found");
    }

    public static Option<string> AsOutputFile(this Option<string> option)
    {
        return AddValidator(
            option,
            static x => !x.AsSpan().ContainsAny(s_invalidPathChars),
            static x => $"The file path '{x}' is invalid");
    }

    public static Option<Uri> AsHttpsUri(this Option<Uri> option)
    {
        return AddValidator(
            option,
            static x => (x is { IsAbsoluteUri: true }) && (x.Scheme == Uri.UriSchemeHttps),
            static x => $"The value '{x}' is not an HTTPS URI");
    }

    private static Argument<T> AddValidator<T>(Argument<T> argument, Predicate<T?> predicate, Func<T?, string> formatter)
    {
        argument.Validators.Add(Validate);

        void Validate(SymbolResult result)
        {
            var value = result.GetValue(argument);

            if (!predicate.Invoke(value))
            {
                result.AddError(formatter.Invoke(value));
            }
        }

        return argument;
    }

    private static Option<T> AddValidator<T>(Option<T> option, Predicate<T?> predicate, Func<T?, string> formatter)
    {
        option.Validators.Add(Validate);

        void Validate(SymbolResult result)
        {
            var value = result.GetValue(option);

            if (!predicate.Invoke(value))
            {
                result.AddError(formatter.Invoke(value));
            }
        }

        return option;
    }
}
