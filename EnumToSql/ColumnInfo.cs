namespace EnumToSql
{
    /// <summary>
    /// Describes information about a column in an enum table.
    /// </summary>
    public class ColumnInfo
    {
        /// <summary>
        /// This is the default name that EnumToSql uses for this column (e.g. "Id" or "DisplayName").
        /// </summary>
        public string CanonicalName { get; }
        /// <summary>
        /// The column name.
        /// </summary>
        public string Name { get; }
        /// <summary>
        /// The size of the column, either in bytes or 2-byte chars, depending on the type.
        /// </summary>
        public int Size { get; }

        internal string SqlName { get; }
        internal string SqlType { get; }
        internal string SizedSqlType { get; }

        internal ColumnInfo(string canonicalName, string name, int size, string sqlType)
        {
            CanonicalName = canonicalName;
            Name = SqlCreator.TrimSqlName(name);
            Size = size;
            SqlName = SqlCreator.BracketSqlName(name);
            SqlType = sqlType;
            SizedSqlType = sqlType;

            if (sqlType == "nvarchar")
                SizedSqlType += "(" + (size == int.MaxValue ? "max" : size.ToString()) + ")";
        }
    }
}