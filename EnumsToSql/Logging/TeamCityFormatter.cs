using System;
using System.Text.RegularExpressions;

namespace EnumsToSql.Logging
{
    class TeamCityFormatter : ILogFormatter
    {
        static readonly Regex s_teamCityEscapeRegex = new Regex(@"['\n\r|\[\]]", RegexOptions.Compiled);

        public string Message(int nestedLevel, Severity severity, string text, string stackTrace = null)
        {
            string statusText;
            var errorDetails = "";
            switch (severity)
            {
                case Severity.Warning:
                    statusText = "WARNING";
                    break;
                case Severity.Error:
                    statusText = "ERROR";
                    errorDetails = stackTrace == null ? "" : $" errorDetails='{Escape(stackTrace)}'";
                    break;
                default:
                    statusText = "NORMAL";
                    break;
            }

            return $"##teamcity[message timestamp='{GetTimestamp()}' text='{Escape(text)}' status='{statusText}'{errorDetails}]{Environment.NewLine}";
        }

        public string OpenBlock(int nestedLevel, string name)
        {
            return $"##teamcity[blockOpened timestamp='{GetTimestamp()}' name='{Escape(name)}']{Environment.NewLine}";
        }

        public string CloseBlock(int nestedLevel, string name, bool lastActionWasBlockOpenOrClose)
        {
            return $"##teamcity[blockClosed timestamp='{GetTimestamp()}' name='{Escape(name)}']{Environment.NewLine}";
        }

        static string GetTimestamp()
        {
            return DateTimeOffset.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff");
        }

        static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s))
                return s;

            return s_teamCityEscapeRegex.Replace(s, MatchReplace);
        }

        static string MatchReplace(Match match)
        {
            switch (match.Value)
            {
                case "'":
                    return "|'";
                case "\n":
                    return "|n";
                case "\r":
                    return "|r";
                case "|":
                    return "||";
                case "[":
                    return "|[";
                case "]":
                    return "|]";
                default:
                    return match.Value;
            }
        }
    }
}