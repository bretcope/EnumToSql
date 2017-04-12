using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using EnumsToSql.Logging;

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
        /// The SQL Server schema name for the enum's table.
        /// </summary>
        public string SchemaName { get; }
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
        public EnumValue[] Values { get; }

        EnumInfo(string tableName, string schemaName, int idColumnSize, string idColumnName, Type type, BackingTypeInfo backingTypeInfo, EnumValue[] values)
        {
            TableName = TrimSqlName(tableName);
            SchemaName = TrimSqlName(schemaName);
            IdColumnSize = idColumnSize;
            IdColumnName = TrimSqlName(idColumnName);
            Type = type;
            BackingTypeInfo = backingTypeInfo;
            Values = values;
        }

        internal static List<EnumInfo> GetEnumsFromAssemblies(IEnumerable<string> assemblyFiles, string attributeName, Logger logger)
        {
            var asmInfos = LoadAssemblies(assemblyFiles, logger);
            return GetEnumInfos(asmInfos, attributeName, logger);
        }

        static List<AssemblyInfo> LoadAssemblies(IEnumerable<string> assemblyFiles, Logger logger)
        {
            var asmInfos = new List<AssemblyInfo>();
            foreach (var filePath in assemblyFiles)
            {
                var info = LoadAssembly(filePath, logger);
                asmInfos.Add(info);
            }
            
            return asmInfos;
        }

        static AssemblyInfo LoadAssembly(string filePath, Logger logger)
        {
            filePath = Path.GetFullPath(filePath);
            using (logger.OpenBlock($"Loading assembly: {filePath}"))
            {
                try
                {
                    var asm = Assembly.LoadFrom(filePath);
                    var xml = GetXmlDocument(filePath);

                    if (xml != null)
                        logger.Info("Found XML documentation");

                    return new AssemblyInfo(asm, xml);
                }
                catch (Exception ex)
                {
                    logger.Exception(ex);
                    throw new EnumsToSqlException("Unable to load assembly", ex, isLogged: true);
                }
            }
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

        static List<EnumInfo> GetEnumInfos(List<AssemblyInfo> asmInfos, string attributeName, Logger logger)
        {
            if (!attributeName.EndsWith("Attribute"))
                attributeName += "Attribute";

            var enumInfos = new List<EnumInfo>();

            foreach (var asmInfo in asmInfos)
            {
                foreach (var type in asmInfo.Assembly.GetTypes())
                {
                    if (!type.IsEnum)
                        continue;

                    XmlTypeDescription xmlDoc = null;
                    asmInfo.XmlDocument?.TypesDescriptions?.TryGetValue(type.FullName, out xmlDoc);

                    var enumInfo = TryGetEnumInfoFromType(type, attributeName, xmlDoc);
                    if (enumInfo != null)
                        enumInfos.Add(enumInfo);
                }
            }

            // log information
            using (logger.OpenBlock($"Found {enumInfos.Count} enums"))
            {
                if (enumInfos.Count > 0)
                {
                    var sb = new StringBuilder();

                    var maxEnumName = enumInfos.Select(ei => ei.FullName.Length).Max();
                    var format = $"    {{0,-{Math.Max(4, maxEnumName)}}}  {{1}}";
                    
                    sb.AppendFormat(format, "Enum", "SQL Table");
                    sb.AppendLine();
                    sb.AppendFormat(format, "----", "---------");
                    foreach (var ei in enumInfos)
                    {
                        sb.AppendLine();
                        sb.AppendFormat(format, ei.FullName, ei.TableName);
                    }

                    logger.Info(sb.ToString());
                }
            }

            return enumInfos;
        }

        static EnumInfo TryGetEnumInfoFromType(Type enumType, string attributeName, XmlTypeDescription xmlDoc)
        {
            var attrInfo = DuckTyping.GetEnumSqlTableInfo(enumType, attributeName);

            if (attrInfo == null) // the enum wasn't marked with the EnumSqlTable attribute
                return null;

            var schemaName = attrInfo.SchemaName;
            if (string.IsNullOrWhiteSpace(schemaName))
                schemaName = "dbo";

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

            return new EnumInfo(attrInfo.TableName, schemaName, idColumnSize, idColumnName, enumType, backingTypeInfo, values);
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

                if (name.Length > EnumValue.MAX_NAME_LENGTH)
                    throw new Exception($"Enum value name exceeds the maximum length of {EnumValue.MAX_NAME_LENGTH}.\n  Enum: {enumType.FullName}\n  Value: {name}");

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

            Array.Sort(values, ValueComparer);

            return values;
        }

        static int ValueComparer(EnumValue a, EnumValue b)
        {
            if (a.LongId < b.LongId)
                return -1;

            if (a.LongId == b.LongId)
                return 0;

            return 1;
        }

        static string TrimSqlName(string name)
        {
            name = name.Trim();
            if (name.StartsWith("[") && name.EndsWith("]") && name.Length > 2)
            {
                name = name.Substring(1, name.Length - 2);
            }

            return name;
        }
    }
}