namespace EnumsToSql
{
    class EnumSqlTableAttributeInfo
    {
        public string TableName { get; }
        public int IdColumnSize { get; }
        public string IdColumnName { get; }

        internal EnumSqlTableAttributeInfo(string tableName, int idColumnSize, string idColumnName)
        {
            TableName = tableName;
            IdColumnSize = idColumnSize;
            IdColumnName = idColumnName;
        }
    }
}