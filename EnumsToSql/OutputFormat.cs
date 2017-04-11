namespace EnumsToSql
{
    /// <summary>
    /// Controls how text output is formatted.
    /// </summary>
    public enum OutputFormat
    {
        /// <summary>
        /// No special formatting is applied.
        /// </summary>
        None = 0,
        /// <summary>
        /// TeamCity block annotations are added to the output to make it collapsable.
        /// </summary>
        TeamCity = 1,
    }
}