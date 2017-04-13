namespace EnumToSql
{
    /// <summary>
    /// Determines what to do when an enum exists in the database, but no longer exists in code.
    /// </summary>
    public enum DeletionMode
    {
        /// <summary>
        /// Marks deleted enum values as inactive.
        /// </summary>
        MarkAsInactive = 0,
        /// <summary>
        /// Attempts to remove deleted enum values from the database, but does not treat foreign key voliations as a failure.
        /// </summary>
        TryDelete,
        /// <summary>
        /// Removes deleted enum values from the database. Foreign key violations, are treated as failures.
        /// </summary>
        Delete,
        /// <summary>
        /// Deleted enum values in the database are ignored.
        /// </summary>
        DoNothing,
    }
}