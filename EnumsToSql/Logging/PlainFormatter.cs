using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace EnumsToSql.Logging
{
    class PlainFormatter : ILogFormatter
    {
        const int MAX_NESTED_LEVEL = 8;

        const string TIMESTAMP_FORMAT = "yyyy-MM-dd HH:mm:ss.fff  "; // if you edit this, make sure the next line is still accurate
        const string TIMESTAMP_PLACEHOLDER = "                         "; // must be the same length as the output of GetTimestamp

        // console colors
        const string ESC = "\x1b";
        const string RESET = ESC + "[0m";
        const string GREY = ESC + "[90m";
        const string RED = ESC + "[91m";
        const string GREEN = ESC + "[92m";
        const string YELLOW = ESC + "[93m";
        const string BLUE = ESC + "[94m";
        const string MAGENTA = ESC + "[95m";
        const string CYAN = ESC + "[96m";
        const string WHITE = ESC + "[97m";

        static readonly string[] s_indents = CreateIndents();
        static readonly char[] s_newLineChars = {'\r', '\n'};
        static readonly string[] s_lineSplits = {"\r\n", "\n", "\r"};

        readonly bool _addTimestamps;
        readonly bool _addColors;

        public PlainFormatter(bool addTimestamps, bool addColors)
        {
            _addTimestamps = addTimestamps;
            _addColors = addColors;

            if (addColors)
                EnableColors();
        }

        public string Message(int nestedLevel, Severity severity, string text, string stackTrace = null)
        {
            return Format(nestedLevel, severity, text, stackTrace, false);
        }

        public string OpenBlock(int nestedLevel, string name)
        {
            return Format(nestedLevel, Severity.Info, name, null, true) + Environment.NewLine;
        }

        public string CloseBlock(int nestedLevel, string name, bool lastActionWasBlockOpenOrClose)
        {
            return lastActionWasBlockOpenOrClose ? "" : Environment.NewLine;
        }

        string Format(int nestedLevel, Severity severity, string text, string stackTrace, bool isBlock)
        {
            nestedLevel = Math.Min(nestedLevel, MAX_NESTED_LEVEL);
            var indent = s_indents[nestedLevel];
            var timestamp = _addTimestamps ? GetTimestamp() : "";


            if (severity == Severity.Error)
            {
                text = "ERROR: " + text;

                if (_addColors)
                    text = RED + text + RESET;
            }
            else if (severity == Severity.Warning)
            {
                text = "WARNING: " + text;

                if (_addColors)
                    text = YELLOW + text + RESET;
            }
            else if (isBlock && _addColors)
            {
                if (nestedLevel == 0)
                    text = GREEN + text + RESET;
                else if (nestedLevel == 1)
                    text = CYAN + text + RESET;
            }

            if ((severity != Severity.Error || stackTrace == null) && text.IndexOfAny(s_newLineChars) == -1)
            {
                // the simple case where we only have one line to output
                return timestamp + indent + text + Environment.NewLine;
            }

            // we have multiple lines
            var sb = new StringBuilder();
            var textLines = text.Split(s_lineSplits, StringSplitOptions.None);
            var placeholder = _addTimestamps ? TIMESTAMP_PLACEHOLDER : "";

            for (var i = 0; i < textLines.Length; i++)
            {
                sb.Append(i == 0 ? timestamp : placeholder);
                sb.Append(indent);
                sb.AppendLine(textLines[i]);
            }

            if (severity == Severity.Error && stackTrace != null)
            {
                var stackLines = stackTrace.Split(s_lineSplits, StringSplitOptions.None);

                var stackNest = Math.Min(nestedLevel + 1, MAX_NESTED_LEVEL);
                var stackIndent = s_indents[stackNest];

                foreach (var line in stackLines)
                {
                    sb.Append(placeholder);
                    sb.Append(stackIndent);
                    sb.AppendLine(line);
                }
            }

            return sb.ToString();
        }

        string GetTimestamp()
        {
            var timestamp = DateTimeOffset.UtcNow.ToString(TIMESTAMP_FORMAT);

            if (_addColors)
                timestamp = GREY + timestamp + RESET;

            return timestamp;
        }

        static string[] CreateIndents()
        {
            var indents = new string[MAX_NESTED_LEVEL + 1];
            for (var i = 0; i < indents.Length; i++)
            {
                indents[i] = new string(' ', i * 4);
            }

            return indents;
        }

        static void EnableColors()
        {
            const int STD_OUTPUT_HANDLE = -11;
            var INVALID_HANDLE_VALUE = new IntPtr(-1);
            const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;

            var handle = GetStdHandle(STD_OUTPUT_HANDLE);
            if (handle == INVALID_HANDLE_VALUE)
                return; // colors are probably going to be broken, but nothing we can do

            uint mode;
            if (GetConsoleMode(handle, out mode))
            {
                mode |= ENABLE_VIRTUAL_TERMINAL_PROCESSING;
                SetConsoleMode(handle, mode);
            }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll")]
        static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

        [DllImport("kernel32.dll")]
        static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);
    }
}