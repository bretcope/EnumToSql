namespace EnumToSql
{
    class EnumToSqlAttributeInfo
    {
        public string TableName { get; }
        public string SchemaName { get; }
        public int IdColumnSize { get; }
        public string IdColumnName { get; }

        internal EnumToSqlAttributeInfo(string tableName, string schemaName, int idColumnSize, string idColumnName)
        {
            TableName = tableName;
            SchemaName = schemaName;
            IdColumnSize = idColumnSize;
            IdColumnName = idColumnName;
        }
    }
}