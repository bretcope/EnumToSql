﻿using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;

namespace EnumsToSql
{
    /// <summary>
    /// The top level class for the EnumsToSql library. Create an instance by calling <see cref="Create"/>.
    /// </summary>
    public class EnumsToSqlWriter
    {
        /// <summary>
        /// The list of enums which will be replicated to SQL.
        /// </summary>
        public List<EnumInfo> Enums { get; }

        EnumsToSqlWriter(List<EnumInfo> enums)
        {
            Enums = enums;
        }

        /// <summary>
        /// Creates a new instance of <see cref="EnumsToSqlWriter"/> based on a list of assembly file names.
        /// </summary>
        /// <param name="assemblyFiles">The assembly file names to load (including relative or absolute paths).</param>
        /// <param name="logger">The stream to send logging information to.</param>
        public static EnumsToSqlWriter Create(IEnumerable<string> assemblyFiles, TextWriter logger)
        {
            var enumInfos = EnumInfo.GetEnumsFromAssemblies(assemblyFiles, logger);
            return new EnumsToSqlWriter(enumInfos);
        }

        /// <summary>
        /// Updates the enum tables for a single database.
        /// </summary>
        /// <param name="connectionString">The SQL Server connection string. This must include a specific database to connect to.</param>
        /// <param name="deletionMode">Determines what to do when an enum value exists in the database, but no longer exists in code.</param>
        /// <param name="logger">The stream to send logging information to.</param>
        public void UpdateDatabase(string connectionString, DeletionMode deletionMode, TextWriter logger)
        {
            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                UpdateDatabase(conn, deletionMode, logger);
            }
        }

        /// <summary>
        /// Updates the enum tables for a single database.
        /// </summary>
        /// <param name="conn">An open SQL connection to the database you want to update.</param>
        /// <param name="deletionMode">Determines what to do when an enum value exists in the database, but no longer exists in code.</param>
        /// <param name="logger">The stream to send logging information to.</param>
        public void UpdateDatabase(SqlConnection conn, DeletionMode deletionMode, TextWriter logger)
        {
            logger.WriteLine($"Updating database {conn.Database} on {conn.DataSource}");

            foreach (var enumInfo in Enums)
            {
                logger.WriteLine($"    Updating {enumInfo.SchemaName}.{enumInfo.TableName}");

                var existing = SqlExecutor.GetTableRows(conn, enumInfo);

                //
            }
        }
    }
}