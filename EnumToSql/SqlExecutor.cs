using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Text;
using EnumToSql.Logging;

namespace EnumToSql
{
    class SqlExecutor
    {
        struct SchemaOrdinals
        {
            public int Id;
            public int Name;
            public int DisplayName;
            public int Description;
            public int IsActive;

            public static SchemaOrdinals New()
            {
                var ordinals = new SchemaOrdinals();
                ordinals.Id = -1;
                ordinals.Name = -1;
                ordinals.DisplayName = -1;
                ordinals.Description = -1;
                ordinals.IsActive = -1;

                return ordinals;
            }
        }

        public static List<Row> GetTableRows(SqlConnection conn, EnumInfo info)
        {
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = SqlCreator.CreateTableAndSelect(info);

                using (var rdr = cmd.ExecuteReader())
                {
                    SchemaOrdinals ordinals;
                    if (!TryGetOrdinals(info, rdr, out ordinals))
                        throw new InvalidOperationException($"Table {info.Schema}.{info.Table} does not match the expected schema.");

                    var rows = new List<Row>();
                    var idSize = info.IdColumn.Size;

                    while (rdr.Read())
                    {
                        var id = ReadIdColumn(rdr, ordinals.Id, idSize);
                        
                        var name = ordinals.Name == -1 ? null : rdr.GetString(ordinals.Name);
                        var displayName = ordinals.DisplayName == -1 ? null : rdr.GetString(ordinals.DisplayName);
                        var description = ordinals.Description == -1 ? null : rdr.GetString(ordinals.Description);
                        var isActive = ordinals.IsActive == -1 ? false : rdr.GetBoolean(ordinals.IsActive);

                        rows.Add(new Row(id, name, displayName, description, isActive));
                    }

                    return rows;
                }
            }
        }

        public static void UpdateTable(SqlConnection conn, EnumInfo info, TableUpdatePlan plan, Logger logger)
        {
            if (plan.Add.Count == 0 && plan.Update.Count == 0 && plan.Delete.Count == 0)
                return;

            using (logger.OpenBlock($"Updating {info.Schema}.{info.Table}"))
            {
                try
                {
                    if (plan.Add.Count > 0)
                    {
                        var sql = SqlCreator.GetInsertSql(info);

                        foreach (var row in plan.Add)
                        {
                            ExecuteUpdate(conn, sql, info, row);
                            logger.Info($"Added {row.Name}");
                        }
                    }

                    if (plan.Update.Count > 0)
                    {
                        var sql = SqlCreator.GetUpdateSql(info);

                        foreach (var row in plan.Update)
                        {
                            ExecuteUpdate(conn, sql, info, row);
                            logger.Info($"Updated {row.Name}");
                        }
                    }

                    if (plan.Delete.Count > 0)
                    {
                        string sql, successMessage;

                        if (plan.DeletionMode == DeletionMode.MarkAsInactive)
                        {
                            sql = $"update {info.SqlSchema}.{info.SqlTable} set {info.IsActiveColumn.SqlName} = 0 where {info.IdColumn.SqlName} = @{EnumInfo.ID};";
                            successMessage = "Marked deleted value \"{0}\" as inactive";
                        }
                        else
                        {
                            sql = $"delete from {info.SqlSchema}.{info.SqlTable} where {info.IdColumn.SqlName} = @{EnumInfo.ID};";
                            successMessage = "Deleted {0}";
                        }

                        var ignoreConstraintViolations = plan.DeletionMode == DeletionMode.TryDelete;

                        foreach (var row in plan.Delete)
                        {
                            if (ExecuteDelete(conn, sql, row.Id, ignoreConstraintViolations))
                            {
                                logger.Info(string.Format(successMessage, row.Name));
                            }
                            else
                            {
                                logger.Warning($"Attempted to delete {row.Name}, but failed due to SQL constraints (probably a foreign key)");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.Exception(ex);
                    throw new EnumsToSqlException($"Failed to update table {info.Schema}.{info.Table}", isLogged: true);
                }
            }
        }


        static bool TryGetOrdinals(EnumInfo info, SqlDataReader rdr, out SchemaOrdinals ordinals)
        {
            ordinals = SchemaOrdinals.New();

            var schema = rdr.GetSchemaTable();
            var columns = GetSqlColumns(schema);

            if (columns.Length != info.Columns.Count)
                return false;

            for (var i = 0; i < columns.Length; i++)
            {
                var sqlCol = columns[i];

                if (sqlCol.AllowsNull || sqlCol.IsIdentity)
                    return false;

                ColumnInfo col;

                if (sqlCol.Name == info.IdColumn.Name)
                {
                    ordinals.Id = i;
                    col = info.IdColumn;
                }
                else if (sqlCol.Name == info.NameColumn?.Name)
                {
                    ordinals.Name = i;
                    col = info.NameColumn;
                }
                else if (sqlCol.Name == info.DisplayNameColumn?.Name)
                {
                    ordinals.DisplayName = i;
                    col = info.DisplayNameColumn;
                }
                else if (sqlCol.Name == info.DescriptionColumn?.Name)
                {
                    ordinals.Description = i;
                    col = info.DescriptionColumn;
                }
                else if (sqlCol.Name == info.IsActiveColumn?.Name)
                {
                    ordinals.IsActive = i;
                    col = info.IsActiveColumn;
                }
                else
                {
                    return false;
                }

                if (sqlCol.Type != col.SqlType || sqlCol.Size != col.Size)
                    return false;
            }

            return true;
        }

        static long ReadIdColumn(SqlDataReader rdr, int ordinal, int size)
        {
            switch (size)
            {
                case 1:
                    return rdr.GetByte(ordinal);
                case 2:
                    return rdr.GetInt16(ordinal);
                case 4:
                    return rdr.GetInt32(ordinal);
                case 8:
                    return rdr.GetInt64(ordinal);
                default:
                    throw new Exception($"Unexpected {EnumInfo.ID} column size {size}");
            }
        }

        static void ExecuteUpdate(SqlConnection conn, string sql, EnumInfo info, Row row)
        {
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue(EnumInfo.ID, row.Id);

                if (info.NameColumn != null)
                    cmd.Parameters.AddWithValue(EnumInfo.NAME, row.Name);

                if (info.DisplayNameColumn != null)
                    cmd.Parameters.AddWithValue(EnumInfo.DISPLAY_NAME, row.Name);

                if (info.DescriptionColumn != null)
                    cmd.Parameters.AddWithValue(EnumInfo.DESCRIPTION, row.Description);

                if (info.IsActiveColumn != null)
                    cmd.Parameters.AddWithValue(EnumInfo.IS_ACTIVE, row.IsActive);

                cmd.ExecuteNonQuery();
            }
        }

        static bool ExecuteDelete(SqlConnection conn, string sql, long id, bool ignoreConstraintViolations)
        {
            try
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = sql;
                    cmd.Parameters.AddWithValue(EnumInfo.ID, id);

                    cmd.ExecuteNonQuery();

                    return true;
                }
            }
            catch (SqlException ex) when (ignoreConstraintViolations && ex.Errors.Count > 0 && ex.Errors[0].Number == 547)
            {
                return false;
            }
        }


        struct SqlColumn
        {
            public string Name { get; set; }
            public int Size { get; set; }
            public string Type { get; set; }
            public bool AllowsNull { get; set; }
            public bool IsIdentity { get; set; }
        }

        static SqlColumn[] GetSqlColumns(DataTable schema)
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
            var infos = new SqlColumn[count];

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