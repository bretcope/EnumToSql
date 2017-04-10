using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace EnumsToSql
{
    class SqlExecutor
    {
        const int ID_ORDINAL = 0;
        const int NAME_ORDINAL = 1;
        const int DESC_ORDINAL = 2;
        const int ACTIVE_ORDINAL = 3;

        public static List<Row> GetTableRows(SqlConnection conn, EnumInfo enumInfo)
        {
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = CreateTableAndSelect(enumInfo);

                using (var rdr = cmd.ExecuteReader())
                {
                    if (!IsExpectedSchema(enumInfo, rdr))
                        throw new InvalidOperationException($"Table {enumInfo.SchemaName}{enumInfo.TableName} does not match the expected schema.");

                    var rows = new List<Row>();
                    var idSize = enumInfo.IdColumnSize;

                    while (rdr.Read())
                    {
                        var id = ReadIdColumn(rdr, idSize);
                        var name = rdr.GetString(NAME_ORDINAL);
                        var desc = rdr.GetString(DESC_ORDINAL);
                        var isActive = rdr.GetBoolean(ACTIVE_ORDINAL);

                        rows.Add(new Row(id, name, desc, isActive));
                    }

                    return rows;
                }
            }
        }

        static string CreateTableAndSelect(EnumInfo enumInfo)
        {
            var schemaString = EscapeString(enumInfo.SchemaName);
            var tableString = EscapeString(enumInfo.TableName);

            var schema = EscapeSqlName(enumInfo.SchemaName);
            var table = EscapeSqlName(enumInfo.TableName);
            var id = EscapeSqlName(enumInfo.IdColumnName);
            var idType = GetIdColumnType(enumInfo.IdColumnSize);

            return $@"
if not exists (select * from INFORMATION_SCHEMA.TABLES where TABLE_SCHEMA = '{schemaString}' and TABLE_NAME = '{tableString}')
begin
    create table [{schema}].[{table}]
    (
        [{id}] {idType} not null,
        Name nvarchar({EnumValue.MAX_NAME_LENGTH}) not null,
        Description nvarchar(max) not null,
        IsActive bit not null,

        constraint [PK_{table}] primary key clustered ([{id}])
    );
end

select * from [{schema}].[{table}];
";
        }

        static bool IsExpectedSchema(EnumInfo enumInfo, SqlDataReader rdr)
        {
            var schema = rdr.GetSchemaTable();
            var columns = GetColumnInfos(schema);

            return columns.Length == 4
                   // Id
                   && columns[ID_ORDINAL].Name == enumInfo.IdColumnName
                   && columns[ID_ORDINAL].Size == enumInfo.IdColumnSize
                   && columns[ID_ORDINAL].Type == GetIdColumnType(enumInfo.IdColumnSize)
                   && columns[ID_ORDINAL].AllowsNull == false
                   && columns[ID_ORDINAL].IsIdentity == false
                   // Name
                   && columns[NAME_ORDINAL].Name == "Name"
                   && columns[NAME_ORDINAL].Size == EnumValue.MAX_NAME_LENGTH
                   && columns[NAME_ORDINAL].Type == "nvarchar"
                   && columns[NAME_ORDINAL].AllowsNull == false
                   && columns[NAME_ORDINAL].IsIdentity == false
                   // Description
                   && columns[DESC_ORDINAL].Name == "Description"
                   && columns[DESC_ORDINAL].Size == int.MaxValue
                   && columns[DESC_ORDINAL].Type == "nvarchar"
                   && columns[DESC_ORDINAL].AllowsNull == false
                   && columns[DESC_ORDINAL].IsIdentity == false
                   // IsActive
                   && columns[ACTIVE_ORDINAL].Name == "IsActive"
                   && columns[ACTIVE_ORDINAL].Size == 1
                   && columns[ACTIVE_ORDINAL].Type == "bit"
                   && columns[ACTIVE_ORDINAL].AllowsNull == false
                   && columns[ACTIVE_ORDINAL].IsIdentity == false
                ;
        }

        static long ReadIdColumn(SqlDataReader rdr, int size)
        {
            switch (size)
            {
                case 1:
                    return rdr.GetByte(ID_ORDINAL);
                case 2:
                    return rdr.GetInt16(ID_ORDINAL);
                case 4:
                    return rdr.GetInt32(ID_ORDINAL);
                case 8:
                    return rdr.GetInt64(ID_ORDINAL);
                default:
                    throw new Exception($"Unexpected Id column size {size}");
            }
        }

        static string EscapeSqlName(string name)
        {
            return name.Replace("]", "]]");
        }

        static string EscapeString(string s)
        {
            return s.Replace("'", "''");
        }

        static string GetIdColumnType(int size)
        {
            switch (size)
            {
                case 1:
                    return "tinyint";
                case 2:
                    return "smallint";
                case 4:
                    return "int";
                case 8:
                    return "bigint";
                default:
                    throw new ArgumentOutOfRangeException(nameof(size), $"Unexpected Id column size {size}");
            }
        }

        struct ColumnInfo
        {
            public string Name { get; set; }
            public int Size { get; set; }
            public string Type { get; set; }
            public bool AllowsNull { get; set; }
            public bool IsIdentity { get; set; }
        }

        static ColumnInfo[] GetColumnInfos(DataTable schema)
        {
            var nameIndex = -1;
            var sizeIndex = -1;
            var typeIndex = -1;
            var allowNullIndex = -1;
            var identityIndex = -1;

            for (var i = 0; i < schema.Columns.Count; i++)
            {
                var col = schema.Columns[i];
                switch (col.ColumnName)
                {
                    case "ColumnName":
                        nameIndex = col.Ordinal;
                        break;
                    case "ColumnSize":
                        sizeIndex = col.Ordinal;
                        break;
                    case "DataTypeName":
                        typeIndex = col.Ordinal;
                        break;
                    case "AllowDBNull":
                        allowNullIndex = col.Ordinal;
                        break;
                    case "IsIdentity":
                        identityIndex = col.Ordinal;
                        break;
                }
            }

            var count = schema.Rows.Count;
            var infos = new ColumnInfo[count];

            for (var i = 0; i < count; i++)
            {
                var row = schema.Rows[i];

                infos[i].Name = (string)row[nameIndex];
                infos[i].Size = (int)row[sizeIndex];
                infos[i].Type = (string)row[typeIndex];
                infos[i].AllowsNull = (bool)row[allowNullIndex];
                infos[i].IsIdentity = (bool)row[identityIndex];
            }

            return infos;
        }
    }
}