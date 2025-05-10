// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.CommandLine;
using System.CommandLine.Parsing;

namespace Cotopaxi.Cosmos.PackageManagement.AppHost.Invocation;

internal static class SymbolValidation
{
    public static void AddValidator<T>(this Argument<T> argument, Predicate<T?> predicate, Func<T?, string> formatter)
    {
        argument.AddValidator(ValidateSymbolResult);

        void ValidateSymbolResult(ArgumentResult symbolResult)
        {
            var value = symbolResult.GetValueOrDefault<T>();

            if (!predicate.Invoke(value))
            {
                symbolResult.ErrorMessage = formatter.Invoke(value);
            }
        }
    }

    public static void AddValidator<T>(this Option<T> option, Predicate<T?> predicate, Func<T?, string> formatter)
    {
        option.AddValidator(ValidateSymbolResult);

        void ValidateSymbolResult(OptionResult symbolResult)
        {
            var value = symbolResult.GetValueOrDefault<T>();

            if (!predicate.Invoke(value))
            {
                symbolResult.ErrorMessage = formatter.Invoke(value);
            }
        }
    }
}
