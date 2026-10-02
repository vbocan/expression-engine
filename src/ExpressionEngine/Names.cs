#nullable enable

using System;

namespace ExpressionEngine;

/// <summary>Shared rules about identifiers.</summary>
internal static class Names
{
    public const string True = "true";
    public const string False = "false";

    public static bool IsIdentifierStart(char c) => char.IsLetter(c) || c == '_';

    public static bool IsIdentifierPart(char c) => char.IsLetterOrDigit(c) || c == '_';

    public static bool IsValidIdentifier(string name)
    {
        if (name.Length == 0 || !IsIdentifierStart(name[0]))
        {
            return false;
        }

        for (var i = 1; i < name.Length; i++)
        {
            if (!IsIdentifierPart(name[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Validates a function or constant name; <c>true</c> and <c>false</c> are reserved literals.</summary>
    public static void ValidateIdentifier(string name, string parameterName)
    {
        if (name is null)
        {
            throw new ArgumentNullException(parameterName);
        }

        if (!IsValidIdentifier(name))
        {
            throw new ArgumentException(
                $"'{name}' is not a valid name. Names start with a letter or underscore and contain only letters, digits and underscores.",
                parameterName);
        }

        if (string.Equals(name, True, StringComparison.OrdinalIgnoreCase) || string.Equals(name, False, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"'{name}' is a reserved literal and cannot be used as a name.", parameterName);
        }
    }
}
