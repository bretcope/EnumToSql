using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

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
            var asmInfos = LoadAssemblies(assemblyFiles, logger);
            var enumInfos = GetEnumInfos(asmInfos, logger);

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
            //
        }

        static List<AssemblyInfo> LoadAssemblies(IEnumerable<string> assemblyFiles, TextWriter logger)
        {
            var asmInfos = new List<AssemblyInfo>();
            foreach (var filePath in assemblyFiles)
            {
                var info = LoadAssembly(filePath, logger);
                asmInfos.Add(info);
            }

            logger.WriteLine();
            return asmInfos;
        }

        static AssemblyInfo LoadAssembly(string filePath, TextWriter logger)
        {
            filePath = Path.GetFullPath(filePath);
            logger.WriteLine($"Loading assembly: {filePath}");

            var asm = Assembly.LoadFrom(filePath);
            var xml = GetXmlDocument(filePath);

            if (xml != null)
                logger.WriteLine("    Found XML documentation");

            return new AssemblyInfo(asm, xml);
        }

        static XmlAssemblyDocument GetXmlDocument(string assemblyFilePath)
        {
            // check for an XML file for this assembly in a case-insensitive way, even though Windows is generally case-insensitive
            var dir = Path.GetDirectoryName(assemblyFilePath);
            var filePattern = Path.GetFileNameWithoutExtension(assemblyFilePath) + ".*";

            foreach (var file in Directory.GetFiles(dir, filePattern, SearchOption.TopDirectoryOnly))
            {
                if (string.Compare(Path.GetExtension(file), ".xml", StringComparison.InvariantCultureIgnoreCase) == 0)
                {
                    return XmlAssemblyDocument.GetFromFile(file);
                }
            }

            return null;
        }

        static List<EnumInfo> GetEnumInfos(List<AssemblyInfo> asmInfos, TextWriter logger)
        {
            var enumInfos = new List<EnumInfo>();

            foreach (var asmInfo in asmInfos)
            {
                foreach (var type in asmInfo.Assembly.GetTypes())
                {
                    if (!type.IsEnum)
                        continue;

                    XmlTypeDescription xmlDoc = null;
                    asmInfo.XmlDocument?.TypesDescriptions?.TryGetValue(type.FullName, out xmlDoc);

                    var enumInfo = TryGetEnumInfoFromType(type, xmlDoc);
                    if (enumInfo != null)
                        enumInfos.Add(enumInfo);
                }
            }

            // log information
            logger.WriteLine($"Found {enumInfos.Count} enums:");
            var maxEnumName = enumInfos.Select(ei => ei.FullName.Length).Max();
            var format = $"    {{0,-{Math.Max(4, maxEnumName)}}}  {{1}}";

            logger.WriteLine();
            logger.WriteLine(format, "Enum", "SQL Table");
            logger.WriteLine(format, "----", "---------");
            foreach (var ei in enumInfos)
            {
                logger.WriteLine(format, ei.FullName, ei.TableName);
            }
            logger.WriteLine();

            return enumInfos;
        }

        static EnumInfo TryGetEnumInfoFromType(Type enumType, XmlTypeDescription xmlDoc)
        {
            var attrInfo = DuckTyping.GetEnumSqlTableInfo(enumType);

            if (attrInfo == null) // the enum wasn't marked with the EnumSqlTable attribute
                return null;

            var backingTypeInfo = BackingTypeInfo.Get(enumType);

            var idColumnSize = attrInfo.IdColumnSize;
            if (idColumnSize == 0)
            {
                idColumnSize = backingTypeInfo.Size;
            }
            else if (idColumnSize < backingTypeInfo.Size)
            {
                throw new Exception($"IdColumnSize ({idColumnSize}) is smaller than the backing type for enum {enumType.FullName}");
            }

            var idColumnName = attrInfo.IdColumnName;
            if (string.IsNullOrWhiteSpace(idColumnName))
                idColumnName = "Id";

            var values = GetEnumValues(enumType, xmlDoc);

            return new EnumInfo(attrInfo.TableName, idColumnSize, idColumnName, enumType, backingTypeInfo, values);
        }

        static EnumValue[] GetEnumValues(Type enumType, XmlTypeDescription xmlDoc)
        {
            var fields = enumType.GetFields(BindingFlags.Public | BindingFlags.Static);
            var values = new EnumValue[fields.Length];

            for (var i = 0; i < fields.Length; i++)
            {
                var field = fields[i];

                var id = field.GetRawConstantValue();
                var name = field.Name;
                var isActive = field.GetCustomAttribute<ObsoleteAttribute>() == null;
                
                // first preference is to get the description from XML comments
                string description = null;
                xmlDoc?.FieldSummaries?.TryGetValue(name, out description);

                if (string.IsNullOrWhiteSpace(description))
                {
                    // second choice is the Description attribute
                    description = field.GetCustomAttribute<DescriptionAttribute>()?.Description;

                    if (string.IsNullOrWhiteSpace(description))
                    {
                        //  sadly we don't have any description available
                        description = "";
                    }
                }

                values[i] = new EnumValue(id, name, isActive, description);
            }

            return values;
        }
    }
}