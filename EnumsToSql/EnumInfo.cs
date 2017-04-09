using System;
using System.Collections.Generic;

namespace EnumsToSql
{
    /// <summary>
    /// Describes informtation about an enum which should be replicated to SQL.
    /// </summary>
    public class EnumInfo
    {
        /// <summary>
        /// The enum's full name (including namespace).
        /// </summary>
        public string FullName => Type.FullName;
        /// <summary>
        /// The name of the SQL table to replicate this enum to.
        /// </summary>
        public string TableName { get; }
        /// <summary>
        /// The size (in bytes) of the Id column for the table. Must be 1, 2, 4, or 8.
        /// </summary>
        public int IdColumnSize { get; }
        /// <summary>
        /// The name of the Id column for the table.
        /// </summary>
        public string IdColumnName { get; }
        /// <summary>
        /// The enum type.
        /// </summary>
        public Type Type { get; }
        /// <summary>
        /// The enum's backing type.
        /// </summary>
        public BackingTypeInfo BackingTypeInfo { get; }
        /// <summary>
        /// Information about the enum's values.
        /// </summary>
        public List<EnumValue> Values { get; }

        internal EnumInfo(string tableName, int idColumnSize, string idColumnName, Type type, BackingTypeInfo backingTypeInfo, List<EnumValue> values)
        {
            TableName = tableName;
            IdColumnSize = idColumnSize;
            IdColumnName = idColumnName;
            Type = type;
            BackingTypeInfo = backingTypeInfo;
            Values = values;
        }
    }
}