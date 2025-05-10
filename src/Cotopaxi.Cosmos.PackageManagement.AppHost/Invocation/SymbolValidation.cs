// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.CommandLine;
using System.CommandLine.Parsing;
using System.Diagnostics;

namespace Cotopaxi.Cosmos.PackageManagement.AppHost.Invocation;

internal static class SymbolValidation
{
    public static void AddValidation<T>(this Argument<T> argument, Predicate<T?> predicate, Func<T?, string> formatter)
    {
        Debug.Assert(argument is not null);
        Debug.Assert(predicate is not null);
        Debug.Assert(formatter is not null);

        argument.AddValidator(ValidateSymbolResult);

        void ValidateSymbolResult(ArgumentResult symbolResult)
        {
            Debug.Assert(symbolResult is not null);

            var value = symbolResult.GetValueOrDefault<T>();

            if (!predicate.Invoke(value))
            {
                symbolResult.ErrorMessage = formatter.Invoke(value);
            }
        }
    }

    public static void AddValidation<T>(this Option<T> option, Predicate<T?> predicate, Func<T?, string> formatter)
    {
        Debug.Assert(option is not null);
        Debug.Assert(predicate is not null);
        Debug.Assert(formatter is not null);

        option.AddValidator(ValidateSymbolResult);

        void ValidateSymbolResult(OptionResult symbolResult)
        {
            Debug.Assert(symbolResult is not null);

            var value = symbolResult.GetValueOrDefault<T>();

            if (!predicate.Invoke(value))
            {
                symbolResult.ErrorMessage = formatter.Invoke(value);
            }
        }
    }
}
