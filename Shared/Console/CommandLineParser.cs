using System;
using System.Collections.Generic;
using System.Text;

namespace DedicatedServerMod.Shared.ConsoleSupport
{
    /// <summary>
    /// Represents a parsed raw command line.
    /// </summary>
    public sealed class ParsedCommandLine
    {
        /// <summary>
        /// Initializes a new parsed command line instance.
        /// </summary>
        /// <param name="commandWord">The normalized command word.</param>
        /// <param name="arguments">The parsed argument list.</param>
        public ParsedCommandLine(string commandWord, IReadOnlyList<string> arguments)
        {
            CommandWord = commandWord ?? throw new ArgumentNullException(nameof(commandWord));
            Arguments = arguments ?? throw new ArgumentNullException(nameof(arguments));
        }

        /// <summary>
        /// Gets the normalized command word.
        /// </summary>
        public string CommandWord { get; }

        /// <summary>
        /// Gets the parsed arguments.
        /// </summary>
        public IReadOnlyList<string> Arguments { get; }
    }

    /// <summary>
    /// Represents the result of parsing a raw command line.
    /// </summary>
    public sealed class CommandLineParseResult
    {
        private CommandLineParseResult(bool success, bool isEmpty, ParsedCommandLine commandLine, string errorMessage)
        {
            Success = success;
            IsEmpty = isEmpty;
            CommandLine = commandLine;
            ErrorMessage = errorMessage;
        }

        /// <summary>
        /// Gets whether parsing completed successfully.
        /// </summary>
        public bool Success { get; }

        /// <summary>
        /// Gets whether the input was empty after trimming whitespace.
        /// </summary>
        public bool IsEmpty { get; }

        /// <summary>
        /// Gets the parsed command line when parsing succeeds.
        /// </summary>
        public ParsedCommandLine CommandLine { get; }

        /// <summary>
        /// Gets the parse error message when parsing fails.
        /// </summary>
        public string ErrorMessage { get; }

        /// <summary>
        /// Creates a successful parse result.
        /// </summary>
        public static CommandLineParseResult FromSuccess(ParsedCommandLine commandLine)
        {
            return new CommandLineParseResult(success: true, isEmpty: false, commandLine, errorMessage: null);
        }

        /// <summary>
        /// Creates an empty parse result.
        /// </summary>
        public static CommandLineParseResult FromEmpty()
        {
            return new CommandLineParseResult(success: true, isEmpty: true, commandLine: null, errorMessage: null);
        }

        /// <summary>
        /// Creates a failed parse result.
        /// </summary>
        public static CommandLineParseResult FromError(string errorMessage)
        {
            return new CommandLineParseResult(success: false, isEmpty: false, commandLine: null, errorMessage);
        }
    }

    /// <summary>
    /// Parses line-oriented host console command input.
    /// </summary>
    public static class CommandLineParser
    {
        /// <summary>
        /// Attempts to parse a raw command line.
        /// </summary>
        /// <param name="rawLine">The raw line to parse.</param>
        /// <returns>The parse result.</returns>
        public static CommandLineParseResult TryParse(string rawLine)
        {
            if (string.IsNullOrWhiteSpace(rawLine))
            {
                return CommandLineParseResult.FromEmpty();
            }

            List<string> tokens = new List<string>();
            StringBuilder currentToken = new StringBuilder();
            bool tokenStarted = false;
            bool inSingleQuotes = false;
            bool inDoubleQuotes = false;
            string trimmedLine = rawLine.Trim();

            for (int i = 0; i < trimmedLine.Length; i++)
            {
                char current = trimmedLine[i];

                if (inSingleQuotes)
                {
                    if (current == '\\' && i + 1 < trimmedLine.Length)
                    {
                        char next = trimmedLine[i + 1];
                        if (next == '\'' || next == '\\')
                        {
                            currentToken.Append(next);
                            i++;
                            continue;
                        }
                    }

                    if (current == '\'')
                    {
                        inSingleQuotes = false;
                        continue;
                    }

                    currentToken.Append(current);
                    continue;
                }

                if (inDoubleQuotes)
                {
                    if (current == '\\' && i + 1 < trimmedLine.Length)
                    {
                        char next = trimmedLine[i + 1];
                        if (next == '"' || next == '\\')
                        {
                            currentToken.Append(next);
                            i++;
                            continue;
                        }
                    }

                    if (current == '"')
                    {
                        inDoubleQuotes = false;
                        continue;
                    }

                    currentToken.Append(current);
                    continue;
                }

                if (char.IsWhiteSpace(current))
                {
                    if (tokenStarted)
                    {
                        tokens.Add(currentToken.ToString());
                        currentToken.Clear();
                        tokenStarted = false;
                    }

                    continue;
                }

                if (current == '\'')
                {
                    inSingleQuotes = true;
                    tokenStarted = true;
                    continue;
                }

                if (current == '"')
                {
                    inDoubleQuotes = true;
                    tokenStarted = true;
                    continue;
                }

                currentToken.Append(current);
                tokenStarted = true;
            }

            if (inSingleQuotes || inDoubleQuotes)
            {
                return CommandLineParseResult.FromError("Unterminated quoted argument.");
            }

            if (tokenStarted)
            {
                tokens.Add(currentToken.ToString());
            }

            if (tokens.Count == 0)
            {
                return CommandLineParseResult.FromEmpty();
            }

            string commandWord = tokens[0].ToLowerInvariant();
            List<string> arguments = new List<string>();
            for (int i = 1; i < tokens.Count; i++)
            {
                arguments.Add(tokens[i]);
            }

            return CommandLineParseResult.FromSuccess(new ParsedCommandLine(commandWord, arguments));
        }

        /// <summary>
        /// Builds a deterministic raw command line from a parsed command.
        /// </summary>
        /// <param name="commandLine">The parsed command line.</param>
        /// <returns>The serialized line.</returns>
        public static string BuildLine(ParsedCommandLine commandLine)
        {
            if (commandLine == null)
            {
                throw new ArgumentNullException(nameof(commandLine));
            }

            List<string> parts = new List<string> { EscapeToken(commandLine.CommandWord) };
            for (int i = 0; i < commandLine.Arguments.Count; i++)
            {
                parts.Add(EscapeToken(commandLine.Arguments[i]));
            }

            return string.Join(" ", parts);
        }

        private static string EscapeToken(string token)
        {
            if (token == null || token.Length == 0)
            {
                return "\"\"";
            }

            bool requiresQuotes = false;
            for (int i = 0; i < token.Length; i++)
            {
                char current = token[i];
                if (char.IsWhiteSpace(current) || current == '"' || current == '\'' || current == '\\')
                {
                    requiresQuotes = true;
                    break;
                }
            }

            if (!requiresQuotes)
            {
                return token;
            }

            StringBuilder escaped = new StringBuilder(token.Length + 2);
            escaped.Append('"');
            for (int i = 0; i < token.Length; i++)
            {
                char current = token[i];
                if (current == '"' || current == '\\')
                {
                    escaped.Append('\\');
                }

                escaped.Append(current);
            }

            escaped.Append('"');
            return escaped.ToString();
        }
    }
}
