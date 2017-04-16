using System;
using System.Diagnostics;
using System.Text;
using EnumToSql.Logging;

namespace EnumToSql
{
    static class SqlCreator
    {
        internal static string BracketSqlName(string name)
        {
            if (name == null)
                return null;

            return "[" + name.Replace("]", "]]") + "]";
        }

        internal static string TrimSqlName(string name)
        {
            if (name == null)
                return null;

            name = name.Trim();
            if (name.StartsWith("[") && name.EndsWith("]") && name.Length > 2)
            {
                name = name.Substring(1, name.Length - 2);
            }

            return name;
        }

        internal static string GetIntegerColumnType(int size)
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

        internal static string CreateTableAndSelect(EnumInfo info)
        {
            var sql = new StringBuilder(800);

            sql.Append("if not exists (select * from INFORMATION_SCHEMA.TABLES where TABLE_SCHEMA = '");
            sql.Append(EscapeString(info.Schema));
            sql.Append("' and TABLE_NAME = '");
            sql.Append(EscapeString(info.Table));
            sql.Append("')");
            sql.AppendLine();

            sql.AppendLine("begin");

            CreateTableSql(sql, info);

            sql.AppendLine("end");
            sql.AppendLine();

            sql.Append("select * from ");
            sql.Append(info.SqlSchema);
            sql.Append('.');
            sql.Append(info.SqlTable);
            sql.Append(" order by ");
            sql.Append(info.IdColumn.SqlName);
            sql.Append(" asc;");
            sql.AppendLine();

            return sql.ToString();
        }

        static void CreateTableSql(StringBuilder sql, EnumInfo info)
        {
            const string indent = "    ";

            sql.Append(indent + "create table ");
            sql.Append(info.SqlSchema);
            sql.Append('.');
            sql.Append(info.SqlTable);
            sql.AppendLine();
            sql.AppendLine(indent + "(");

            foreach (var col in info.Columns)
            {
                sql.Append(indent + indent);
                sql.Append(col.SqlName);
                sql.Append(' ');
                sql.Append(col.SizedSqlType);
                sql.Append(" not null,");
                sql.AppendLine();
            }

            sql.AppendLine();
            sql.Append(indent + indent);
            sql.Append("constraint [PK_");
            sql.Append(TrimSqlName(info.Table));
            sql.Append("] primary key clustered (");
            sql.Append(info.IdColumn.SqlName);
            sql.Append(')');
            sql.AppendLine();

            sql.AppendLine(indent + ");");
        }

        internal static string GetInsertSql(EnumInfo info)
        {
            var sql = new StringBuilder(300);
            sql.Append("insert into ");
            sql.Append(info.SqlSchema);
            sql.Append('.');
            sql.Append(info.SqlTable);
            sql.Append(" (");

            foreach (var col in info.Columns)
            {
                sql.Append(col.SqlName);
                sql.Append(',');
            }

            sql[sql.Length - 1] = ')'; // replace last comma with close paren

            sql.Append(" values (");

            foreach (var col in info.Columns)
            {
                sql.Append('@');
                sql.Append(col.CanonicalName);
                sql.Append(',');
            }

            sql[sql.Length - 1] = ')'; // replace last comma with close paren
            sql.Append(';');

            return sql.ToString();
        }

        internal static string GetUpdateSql(EnumInfo info)
        {
            if (info.Columns.Count < 2)
                throw new EnumsToSqlException($"{nameof(GetUpdateSql)} called for an enum with only the Id column. This is a bug in EnumToSql.");

            if (info.Columns[0].CanonicalName != EnumInfo.ID)
                throw new EnumsToSqlException($"Expected {EnumInfo.ID} to be the first column. This is a bug in EnumToSql.");

            var sql = new StringBuilder(300);
            sql.Append("update ");
            sql.Append(info.SqlSchema);
            sql.Append('.');
            sql.Append(info.SqlTable);
            sql.Append(" set ");

            // the first column is always the Id column
            for (var i = 1; i < info.Columns.Count; i++)
            {
                var col = info.Columns[i];

                sql.Append(col.SqlName);
                sql.Append(" = @");
                sql.Append(col.CanonicalName);
                sql.Append(',');
            }

            sql[sql.Length - 1] = ' '; // replace the last comma with a space

            sql.Append("where ");
            sql.Append(info.IdColumn.SqlName);
            sql.Append(" = @" + EnumInfo.ID + ";");

            return sql.ToString();
        }

        static string EscapeString(string s)
        {
            return s.Replace("'", "''");
        }
    }
}