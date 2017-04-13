namespace EnumToSql.Logging
{
    /// <summary>
    /// Defines the interface for formatting log messages.
    /// </summary>
    public interface ILogFormatter
    {
        /// <summary>
        /// Formats a single log message.
        /// </summary>
        /// <param name="nestedLevel">How many blocks this message is a child of.</param>
        /// <param name="severity">The severity of the message.</param>
        /// <param name="text">The message itself.</param>
        /// <param name="stackTrace">If the message is an error, use this parameter to provide stack trace information.</param>
        /// <returns>The formatted string (including new line at the end).</returns>
        string Message(int nestedLevel, Severity severity, string text, string stackTrace = null);

        /// <summary>
        /// Opens a new logging block.
        /// </summary>
        /// <param name="nestedLevel">How many blocks this block is a child of.</param>
        /// <param name="name">The name of the block (should be included in output).</param>
        /// <returns>The text to write to the logging stream.</returns>
        string OpenBlock(int nestedLevel, string name);

        /// <summary>
        /// Closes a logging block. The <paramref name="nestedLevel"/> and <paramref name="name"/> arguments will be the same as when <see cref="OpenBlock"/>
        /// was called.
        /// </summary>
        /// <param name="nestedLevel">How many blocks this block is a child of.</param>
        /// <param name="name">The name of the block (should be included in output).</param>
        /// <param name="lastActionWasBlockOpenOrClose">True if another block was opened or immediately preceeded this block closure.</param>
        /// <returns>The text to write to the logging stream.</returns>
        string CloseBlock(int nestedLevel, string name, bool lastActionWasBlockOpenOrClose);
    }
}