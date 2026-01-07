//
//  MiniJSON.cs
//
//  Created by Calvin Rien on 01/29/2012.
//  Copyright 2012 Calvin Rien
//
// This software is provided 'as-is', without any express or implied warranty. In
// no event will the authors be held liable for any damages arising from the use of
// this software.
//
// Permission is granted to anyone to use this software for any purpose,
// including commercial applications, and to alter it and redistribute it freely,
// subject to the following restrictions:
//
// 1. The origin of this software must not be misrepresented; you must not claim
// that you wrote the original software. If you use this software in a product,
// an acknowledgment in the product documentation would be appreciated but is not
// required.
//
// 2. Altered source versions must be plainly marked as such, and must not be
// misrepresented as being the original software.
//
// 3. This notice may not be removed or altered from any source distribution.
//
/*
 * ------
 *
 * Modified by Trivial Interactive
 * - Changed to a namespaced class.
 * - Optimized parsing by using StringBuilder.
 * - Other minor optimizations.
 *
 * ------
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// This class encodes and decodes JSON strings.
/// Spec. details, see http://www.json.org/
///
/// JSON uses Arrays and Objects. These correspond here to the datatypes IList and IDictionary.
/// All numbers are parsed to doubles.
/// </summary>
namespace RVA.TAC.Utils
{
    public static class MiniJSON
{
    private const int TOKEN_NONE = 0;
    private const int TOKEN_CURLY_OPEN = 1;
    private const int TOKEN_CURLY_CLOSE = 2;
    private const int TOKEN_SQUARED_OPEN = 3;
    private const int TOKEN_SQUARED_CLOSE = 4;
    private const int TOKEN_COLON = 5;
    private const int TOKEN_COMMA = 6;
    private const int TOKEN_STRING = 7;
    private const int TOKEN_NUMBER = 8;
    private const int TOKEN_TRUE = 9;
    private const int TOKEN_FALSE = 10;
    private const int TOKEN_NULL = 11;
    private const int BUILDER_CAPACITY = 2000;

    /// <summary>
    /// On decoding, this value holds the position at which the parse failed (-1 = no error).
    /// </summary>
    private static int lastErrorIndex = -1;
    private static string lastDecode;

    /// <summary>
    /// Parses the string json into a value
    /// </summary>
    /// <param name="json">A JSON string.</param>
    /// <returns>An IList, a IDictionary, a double, a string, null, true, or false</returns>
    public static object Decode(string json)
    {
        // Save the string for debug information
        lastDecode = json;

        if (json != null)
        {
            char[] charArray = json.ToCharArray();
            int index = 0;
            bool success = true;
            object value = ParseValue(charArray, ref index, ref success);

            if (success)
            {
                lastErrorIndex = -1;
            }
            else
            {
                lastErrorIndex = index;
            }

            return value;
        }

        return null;
    }

    /// <summary>
    /// Converts a IDictionary / IList object into a JSON string
    /// </summary>
    /// <param name="json">A IDictionary / IList</param>
    /// <returns>A JSON string</returns>
    public static string Encode(object json)
    {
        var builder = new StringBuilder(BUILDER_CAPACITY);
        var success = SerializeValue(json, builder);

        return (success ? builder.ToString() : null);
    }

    private static Dictionary<string, object> ParseObject(char[] json, ref int index, ref bool success)
    {
        var table = new Dictionary<string, object>();
        int token;

        // {
        NextToken(json, ref index);

        while (true)
        {
            token = LookAhead(json, index);
            if (token == TOKEN_NONE)
            {
                success = false;
                return null;
            }

            if (token == TOKEN_CURLY_CLOSE)
            {
                NextToken(json, ref index);
                return table;
            }

            if (token == TOKEN_COMMA)
            {
                NextToken(json, ref index);
            }
            else
            {
                // "name"
                string name = ParseString(json, ref index, ref success);
                if (!success)
                {
                    success = false;
                    return null;
                }

                // :
                token = NextToken(json, ref index);
                if (token != TOKEN_COLON)
                {
                    success = false;
                    return null;
                }

                // value
                object value = ParseValue(json, ref index, ref success);
                if (!success)
                {
                    success = false;
                    return null;
                }

                table[name] = value;
            }
        }
    }

    private static List<object> ParseArray(char[] json, ref int index, ref bool success)
    {
        var array = new List<object>();

        // [
        NextToken(json, ref index);

        while (true)
        {
            int token = LookAhead(json, index);
            if (token == TOKEN_NONE)
            {
                success = false;
                return null;
            }

            if (token == TOKEN_SQUARED_CLOSE)
            {
                NextToken(json, ref index);
                break;
            }

            if (token == TOKEN_COMMA)
            {
                NextToken(json, ref index);
            }
            else
            {
                object value = ParseValue(json, ref index, ref success);
                if (!success)
                {
                    return null;
                }

                array.Add(value);
            }
        }

        return array;
    }

    private static object ParseValue(char[] json, ref int index, ref bool success)
    {
        switch (LookAhead(json, index))
        {
            case TOKEN_CURLY_OPEN:
                return ParseObject(json, ref index, ref success);
            case TOKEN_SQUARED_OPEN:
                return ParseArray(json, ref index, ref success);
            case TOKEN_STRING:
                return ParseString(json, ref index, ref success);
            case TOKEN_NUMBER:
                return ParseNumber(json, ref index, ref success);
            case TOKEN_TRUE:
                NextToken(json, ref index);
                return true;
            case TOKEN_FALSE:
                NextToken(json, ref index);
                return false;
            case TOKEN_NULL:
                NextToken(json, ref index);
                return null;
            case TOKEN_NONE:
                break;
        }

        success = false;
        return null;
    }

    private static string ParseString(char[] json, ref int index, ref bool success)
    {
        var s = new StringBuilder(BUILDER_CAPACITY);
        char c;

        EatWhitespace(json, ref index);

        // "
        c = json[index++];

        bool complete = false;
        while (!complete)
        {
            if (index == json.Length)
            {
                break;
            }

            c = json[index++];
            if (c == '"')
            {
                complete = true;
                break;
            }

            if (c == '\\')
            {
                if (index == json.Length)
                {
                    break;
                }

                c = json[index++];
                if (c == '"')
                {
                    s.Append('"');
                }
                else if (c == '\\')
                {
                    s.Append('\\');
                }
                else if (c == '/')
                {
                    s.Append('/');
                }
                else if (c == 'b')
                {
                    s.Append('\b');
                }
                else if (c == 'f')
                {
                    s.Append('\f');
                }
                else if (c == 'n')
                {
                    s.Append('\n');
                }
                else if (c == 'r')
                {
                    s.Append('\r');
                }
                else if (c == 't')
                {
                    s.Append('\t');
                }
                else if (c == 'u')
                {
                    int remainingLength = json.Length - index;
                    if (remainingLength >= 4)
                    {
                        char[] unicodeCharArray = new char[4];
                        Array.Copy(json, index, unicodeCharArray, 0, 4);

                        // Drop in the HTML markup for the unicode character
                        s.Append(string.Format("&#x{0};", new string(unicodeCharArray)));

                        // skip 4 chars
                        index += 4;
                    }
                    else
                    {
                        break;
                    }
                }
            }
            else
            {
                s.Append(c);
            }
        }

        if (!complete)
        {
            success = false;
            return null;
        }

        return s.ToString();
    }

    private static object ParseNumber(char[] json, ref int index, ref bool success)
    {
        EatWhitespace(json, ref index);

        int lastIndex = GetLastIndexOfNumber(json, index);
        int charLength = (lastIndex - index) + 1;
        char[] numberCharArray = new char[charLength];

        Array.Copy(json, index, numberCharArray, 0, charLength);
        index = lastIndex + 1;

        string numberStr = new string(numberCharArray);
        if (numberStr.Contains("."))
        {
            double number;
            success = double.TryParse(numberStr, out number);
            return number;
        }

        long integer;
        success = long.TryParse(numberStr, out integer);
        return integer;
    }

    private static int GetLastIndexOfNumber(char[] json, int index)
    {
        int lastIndex;

        for (lastIndex = index; lastIndex < json.Length; lastIndex++)
        {
            if ("0123456789+-.eE".IndexOf(json[lastIndex]) == -1)
            {
                break;
            }
        }
        return lastIndex - 1;
    }

    private static void EatWhitespace(char[] json, ref int index)
    {
        while (index < json.Length)
        {
            if (" \t\n\r".IndexOf(json[index]) == -1)
            {
                break;
            }

            index++;
        }
    }

    private static int LookAhead(char[] json, int index)
    {
        int saveIndex = index;
        return NextToken(json, ref saveIndex);
    }

    private static int NextToken(char[] json, ref int index)
    {
        EatWhitespace(json, ref index);

        if (index == json.Length)
        {
            return TOKEN_NONE;
        }

        char c = json[index];
        index++;
        switch (c)
        {
            case '{':
                return TOKEN_CURLY_OPEN;
            case '}':
                return TOKEN_CURLY_CLOSE;
            case '[':
                return TOKEN_SQUARED_OPEN;
            case ']':
                return TOKEN_SQUARED_CLOSE;
            case ',':
                return TOKEN_COMMA;
            case '"':
                return TOKEN_STRING;
            case ':':
                return TOKEN_COLON;
            case '0':
            case '1':
            case '2':
            case '3':
            case '4':
            case '5':
            case '6':
            case '7':
            case '8':
            case '9':
            case '-':
                return TOKEN_NUMBER;
        }

        index--;

        int remainingLength = json.Length - index;
        // false
        if (remainingLength >= 5)
        {
            if (json[index] == 'f' &&
                json[index + 1] == 'a' &&
                json[index + 2] == 'l' &&
                json[index + 3] == 's' &&
                json[index + 4] == 'e')
            {
                index += 5;
                return TOKEN_FALSE;
            }
        }

        // true
        if (remainingLength >= 4)
        {
            if (json[index] == 't' &&
                json[index + 1] == 'r' &&
                json[index + 2] == 'u' &&
                json[index + 3] == 'e')
            {
                index += 4;
                return TOKEN_TRUE;
            }
        }

        // null
        if (remainingLength >= 4)
        {
            if (json[index] == 'n' &&
                json[index + 1] == 'u' &&
                json[index + 2] == 'l' &&
                json[index + 3] == 'l')
            {
                index += 4;
                return TOKEN_NULL;
            }
        }

        return TOKEN_NONE;
    }

    private static bool SerializeValue(object value, StringBuilder builder)
    {
        if (value is string)
        {
            SerializeString((string) value, builder);
        }
        else if (value is IDictionary)
        {
            SerializeObject((IDictionary) value, builder);
        }
        else if (value is IList)
        {
            SerializeArray((IList) value, builder);
        }
        else if (IsNumeric(value))
        {
            builder.Append(value);
        }
        else if ((value is bool) && ((bool) value))
        {
            builder.Append("true");
        }
        else if ((value is bool) && !((bool) value))
        {
            builder.Append("false");
        }
        else if (value == null)
        {
            builder.Append("null");
        }
        else
        {
            return false;
        }

        return true;
    }

    private static void SerializeObject(IDictionary anObject, StringBuilder builder)
    {
        builder.Append('{');

        bool first = true;
        foreach (object key in anObject.Keys)
        {
            if (!first)
            {
                builder.Append(',');
            }

            SerializeString(key.ToString(), builder);
            builder.Append(':');
            if (!SerializeValue(anObject[key], builder))
            {
                return;
            }

            first = false;
        }

        builder.Append('}');
    }

    private static void SerializeArray(IList anArray, StringBuilder builder)
    {
        builder.Append('[');

        bool first = true;
        foreach (object value in anArray)
        {
            if (!first)
            {
                builder.Append(',');
            }

            if (!SerializeValue(value, builder))
            {
                return;
            }

            first = false;
        }

        builder.Append(']');
    }

    private static void SerializeString(string aString, StringBuilder builder)
    {
        builder.Append('\"');

        char[] charArray = aString.ToCharArray();
        foreach (var c in charArray)
        {
            if (c == '"')
            {
                builder.Append("\\\"");
            }
            else if (c == '\\')
            {
                builder.Append("\\\\");
            }
            else if (c == '\b')
            {
                builder.Append("\\b");
            }
            else if (c == '\f')
            {
                builder.Append("\\f");
            }
            else if (c == '\n')
            {
                builder.Append("\\n");
            }
            else if (c == '\r')
            {
                builder.Append("\\r");
            }
            else if (c == '\t')
            {
                builder.Append("\\t");
            }
            else
            {
                int codepoint = Convert.ToInt32(c);
                if ((codepoint >= 32) && (codepoint <= 126))
                {
                    builder.Append(c);
                }
                else
                {
                    builder.Append("\\u" + Convert.ToString(codepoint, 16).PadLeft(4, '0'));
                }
            }
        }

        builder.Append('\"');
    }

    private static bool IsNumeric(object o)
    {
        return o is sbyte ||
               o is byte ||
               o is short ||
               o is ushort ||
               o is int ||
               o is uint ||
               o is long ||
               o is ulong ||
               o is float ||
               o is double ||
               o is decimal;
    }
}
