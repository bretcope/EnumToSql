using System.Collections.Generic;
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
        /// The default name of the attribute which marks enums for replication.
        /// </summary>
        public const string DEFAULT_ATTRIBUTE_NAME = "EnumSqlTable";

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
        /// <param name="attributeName">The name of the attribute which marks enums for replication.</param>
        public static EnumsToSqlWriter Create(IEnumerable<string> assemblyFiles, TextWriter logger, string attributeName = DEFAULT_ATTRIBUTE_NAME)
        {
            var enumInfos = EnumInfo.GetEnumsFromAssemblies(assemblyFiles, attributeName, logger);
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
            if (Enums.Count == 0)
                return;

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
            if (Enums.Count == 0)
                return;

            logger.WriteLine($"Updating database {conn.Database} on {conn.DataSource}");

            foreach (var enumInfo in Enums)
            {
                var existingRows = SqlExecutor.GetTableRows(conn, enumInfo);
                var updatePlan = TableUpdatePlan.Create(enumInfo, existingRows, deletionMode);
                SqlExecutor.UpdateTable(conn, enumInfo, updatePlan, logger);
            }
        }
    }
}