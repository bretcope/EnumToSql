using System;

namespace EnumToSql.Logging
{
    /// <summary>
    /// The exception class used by EnumsToSql methods.
    /// </summary>
    public class EnumsToSqlException : Exception
    {
        /// <summary>
        /// Used to indicate whether an exception has already been logged.
        /// </summary>
        internal bool IsLogged { get; set; }

        internal EnumsToSqlException(string message, Exception innerException = null, bool isLogged = false)
            : base(message, innerException)
        {
            IsLogged = isLogged;
        }
    }
}