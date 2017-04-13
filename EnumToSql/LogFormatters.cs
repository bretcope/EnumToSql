using EnumToSql.Logging;

namespace EnumToSql
{
    /// <summary>
    /// Static collection of built-in logging formatters.
    /// </summary>
    public static class LogFormatters
    {
        static PlainFormatter s_plain;
        static PlainFormatter s_timestamps;
        static PlainFormatter s_colors;
        static PlainFormatter s_timestampsColors;
        static TeamCityFormatter s_teamCity;

        /// <summary>
        /// Nested blocks are indented, but no other special formatting is applied.
        /// </summary>
        public static ILogFormatter Plain => s_plain ?? (s_plain = new PlainFormatter(false, false));

        /// <summary>
        /// Messages begin with a timestamp. Nested blocks are indented.
        /// </summary>
        public static ILogFormatter Timestamps => s_timestamps ?? (s_timestamps = new PlainFormatter(true, false));

        /// <summary>
        /// Nested blocks are indented. Messages are color coded.
        /// </summary>
        public static ILogFormatter Colors => s_colors ?? (s_colors = new PlainFormatter(false, true));

        /// <summary>
        /// Messages begin with a timestamp. Nested blocks are indented. Messages are color coded.
        /// </summary>
        public static ILogFormatter TimestampsColors => s_timestampsColors ?? (s_timestampsColors = new PlainFormatter(true, true));

        /// <summary>
        /// Outputs logs using TeamCity's message/block format. https://confluence.jetbrains.com/display/TCD10/Build+Script+Interaction+with+TeamCity
        /// </summary>
        public static ILogFormatter TeamCity => s_teamCity ?? (s_teamCity = new TeamCityFormatter());
    }
}