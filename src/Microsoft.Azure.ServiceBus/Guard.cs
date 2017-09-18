// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.ServiceBus
{
    using System.Linq;
    using System;
    using System.Collections;

    static class Guard
    {
        public static void AgainstNull(string argumentName, object value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(argumentName);
            }
        }

        public static void AgainstNullAndEmpty(string argumentName, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentNullException(argumentName);
            }
        }

        public static void AgainstTooLongWithValue(string argumentName, string value, int maximumLength)
        {
            if (value.Length > maximumLength)
            {
                throw new ArgumentOutOfRangeException(argumentName, value, $"Exceeds the '{maximumLength}' character limit.");
            }
        }

        public static void AgainstTooLong(string argumentName, string value, int maximumLength)
        {
            if (value.Length > maximumLength)
            {
                throw new ArgumentOutOfRangeException(argumentName, message: $"Exceeds the '{maximumLength}' character limit.");
            }
        }

        public static void AgainstNegative(string argumentName, int value)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(argumentName, value, "Value cannot be negative");
            }
        }

        public static void AgainstNegative(string argumentName, TimeSpan value)
        {
            if (value < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(argumentName, value, "Value cannot be negative.");
            }
        }

        public static void AgainstNegativeAndZero(string argumentName, TimeSpan value)
        {
            if (value <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(argumentName, value, "Value must be positive.");
            }
        }

        public static void AgainstInThePast(string argumentName, DateTimeOffset value)
        {
            if (value.CompareTo(DateTimeOffset.UtcNow) < 0)
            {
                throw new ArgumentOutOfRangeException(argumentName, value, "Value cannot be in the past.");
            }
        }

        public static void AgainstNegative(string argumentName, long value)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(argumentName, value, "Value cannot be less than 0.");
            }
        }

        public static void AgainstInvalidCharacters(string argumentName, string value, string message, char[] invalidCharacters)
        {
            foreach (var character in value)
            {
                if (invalidCharacters.Contains(character))
                {
                    throw new ArgumentException($"{message}. Invalid character: {character}. Value: {value}.", argumentName);
                }
            }
        }

        public static void AgainstInvalidCharacters(string argumentName, string value, char[] invalidCharacters)
        {
            foreach (var character in value)
            {
                if (invalidCharacters.Contains(character))
                {
                    throw new ArgumentException($"Invalid character: {character}. Value: {value}.", argumentName);
                }
            }
        }

        public static void AgainstEmpty(string argumentName, ICollection value)
        {
            if (value.Count == 0)
            {
                throw new ArgumentOutOfRangeException(argumentName);
            }
        }

    }
}