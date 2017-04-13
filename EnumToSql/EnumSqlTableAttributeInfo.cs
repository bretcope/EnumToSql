namespace EnumToSql
{
    class EnumSqlTableAttributeInfo
    {
        public string TableName { get; }
        public string SchemaName { get; }
        public int IdColumnSize { get; }
        public string IdColumnName { get; }

        internal EnumSqlTableAttributeInfo(string tableName, string schemaName, int idColumnSize, string idColumnName)
        {
            TableName = tableName;
            SchemaName = schemaName;
            IdColumnSize = idColumnSize;
            IdColumnName = idColumnName;
        }
    }
}