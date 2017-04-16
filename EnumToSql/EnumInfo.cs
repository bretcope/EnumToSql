using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using EnumToSql.Logging;

namespace EnumToSql
{
    /// <summary>
    /// Describes informtation about an enum which should be replicated to SQL.
    /// </summary>
    public class EnumInfo
    {
        internal const string ID = "Id";
        internal const string NAME = "Name";
        internal const string DISPLAY_NAME = "DisplayName";
        internal const string DESCRIPTION = "Description";
        internal const string IS_ACTIVE = "IsActive";

        // these are pre-escaped and bracketed versions of the user-provided names which are appropriate for inlining as table/column names in a query
        internal string SqlTable { get; }
        internal string SqlSchema { get; }

        internal List<ColumnInfo> Columns { get; private set; }

        /// <summary>
        /// The enum type.
        /// </summary>
        public Type Type { get; }
        /// <summary>
        /// The enum's full name (including namespace).
        /// </summary>
        public string FullName => Type.FullName;
        /// <summary>
        /// The enum's backing type.
        /// </summary>
        public BackingTypeInfo BackingTypeInfo { get; }

        /// <summary>
        /// The name of the SQL table to replicate this enum to.
        /// </summary>
        public string Table { get; }
        /// <summary>
        /// The SQL Server schema name for the enum's table.
        /// </summary>
        public string Schema { get; }
        /// <summary>
        /// Controls what happens when an enum value no longer exists in code, but still exists as a database row.
        /// </summary>
        public DeletionMode DeletionMode { get; }

        /// <summary>
        /// Information about the Id column, or null if disabled.
        /// </summary>
        public ColumnInfo IdColumn { get; private set; }
        /// <summary>
        /// Information about the Name column, or null if disabled.
        /// </summary>
        public ColumnInfo NameColumn { get; private set; }
        /// <summary>
        /// Information about the DisplayName column, or null if disabled.
        /// </summary>
        public ColumnInfo DisplayNameColumn { get; private set; }
        /// <summary>
        /// Information about the Description column, or null if disabled.
        /// </summary>
        public ColumnInfo DescriptionColumn { get; private set; }
        /// <summary>
        /// Information about the IsActive column, or null if disabled.
        /// </summary>
        public ColumnInfo IsActiveColumn { get; private set; }

        /// <summary>
        /// Information about the enum's values.
        /// </summary>
        public EnumValue[] Values { get; }

        EnumInfo(Type enumType, AttributeInfo attrInfo, EnumValue[] values)
        {
            Schema = SqlCreator.TrimSqlName(attrInfo.Schema);
            SqlSchema = SqlCreator.BracketSqlName(Schema);

            Table = SqlCreator.TrimSqlName(attrInfo.Table);
            SqlTable = SqlCreator.BracketSqlName(Table);

            if (string.IsNullOrEmpty(Schema))
                throw new EnumsToSqlException($"Schema name cannot be null or empty. Enum: {FullName}");

            if (string.IsNullOrEmpty(Table))
                throw new EnumsToSqlException($"Table name cannot be null or empty. Enum: {FullName}");

            DeletionMode mode;
            if (Enum.TryParse(attrInfo.DeletionMode, true, out mode))
                DeletionMode = mode;
            else
                throw new EnumsToSqlException($"DeletionMode \"{attrInfo.DeletionMode}\" is not valid. Enum: {FullName}");

            Type = enumType;
            BackingTypeInfo = BackingTypeInfo.Get(enumType);

            Values = values;

            SetupColumns(attrInfo);
        }

        void SetupColumns(AttributeInfo attrInfo)
        {
            var columns = new List<ColumnInfo>();

            var idSize = attrInfo.IdColumnSize == 0 ? BackingTypeInfo.Size : attrInfo.IdColumnSize;
            if (idSize < BackingTypeInfo.Size)
                throw new EnumsToSqlException($"IdColumnSize is smaller than the enum's backing type. Enum: {FullName}");

            switch (idSize)
            {
                case 1:
                case 2:
                case 4:
                case 8:
                    break;
                default:
                    throw new InvalidOperationException($"{idSize} is not a valid IdColumnSize. Must be 0 (default), 1, 2, 4, or 8. Enum: {FullName}");
            }

            IdColumn = new ColumnInfo(ID, attrInfo.IdColumn, idSize, SqlCreator.GetIntegerColumnType(idSize));
            columns.Add(IdColumn);

            if (string.IsNullOrEmpty(IdColumn.Name))
                throw new EnumsToSqlException($"Id column name cannot be null or empty. Enum: {FullName}");

            if (attrInfo.NameColumnEnabled)
            {
                var name = attrInfo.NameColumn;
                var size = attrInfo.NameColumnSize;

                NameColumn = new ColumnInfo(NAME, name, size, "nvarchar");
                columns.Add(NameColumn);
                
                if (string.IsNullOrEmpty(NameColumn.Name))
                    throw new EnumsToSqlException($"NameColumn property cannot be null or empty. Enum: {FullName}");

                if (size < 1)
                    throw new EnumsToSqlException($"NameColumnSize cannot be less than 1. Enum: {FullName}");

                foreach (var value in Values)
                {
                    if (value.Name.Length > size)
                        throw new Exception($"Enum value name exceeds the maximum length of {size}.\n  Enum: {FullName}\n  Value: {value.Name}");
                }

            }

            if (attrInfo.DisplayNameColumnEnabled)
            {
                var name = attrInfo.DisplayNameColumn;
                var size = attrInfo.DisplayNameColumnSize;

                DisplayNameColumn = new ColumnInfo(DISPLAY_NAME, name, size, "nvarchar");
                columns.Add(DisplayNameColumn);

                if (string.IsNullOrEmpty(name))
                    throw new EnumsToSqlException($"DisplayNameColumn property cannot be null or empty. Enum: {FullName}");

                if (size < 1)
                    throw new EnumsToSqlException($"DisplayNameColumnSize cannot be less than 1. Enum: {FullName}");

                foreach (var value in Values)
                {
                    if (value.DisplayName.Length > size)
                        throw new Exception($"Enum value display name exceeds the maximum length of {size}.\n  Enum: {FullName}\n  Value: {value.Name}");
                }
            }

            if (attrInfo.DescriptionColumnEnabled)
            {
                var name = attrInfo.DescriptionColumn;
                var size = attrInfo.DescriptionColumnSize;

                DescriptionColumn = new ColumnInfo(DESCRIPTION, name, size, "nvarchar");
                columns.Add(DescriptionColumn);

                if (string.IsNullOrEmpty(DescriptionColumn.Name))
                    throw new EnumsToSqlException($"DescriptionColumn property cannot be null or empty. Enum: {FullName}");

                if (size < 1)
                    throw new EnumsToSqlException($"DescriptionColumnSize cannot be less than 1. Enum: {FullName}");

                foreach (var value in Values)
                {
                    if (value.Description.Length > size)
                        throw new Exception($"Enum value description exceeds the maximum length of {size}.\n  Enum: {FullName}\n  Value: {value.Name}");
                }
            }

            if (attrInfo.IsActiveColumnEnabled)
            {
                IsActiveColumn = new ColumnInfo(IS_ACTIVE, attrInfo.IsActiveColumn, 1, "bit");
                columns.Add(IsActiveColumn);

                if (string.IsNullOrEmpty(IsActiveColumn.Name))
                    throw new EnumsToSqlException($"IsActiveColumn property cannot be null or empty. Enum: {FullName}");
            }
            else if (DeletionMode == DeletionMode.MarkAsInactive)
            {
                throw new EnumsToSqlException($"DeletionMode is {DeletionMode.MarkAsInactive}, but the {IS_ACTIVE} column is disabled. Enum: {FullName}");
            }

            Columns = columns;
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
                        sb.AppendFormat(format, ei.FullName, ei.Table);
                    }

                    logger.Info(sb.ToString());
                }
            }

            return enumInfos;
        }

        static EnumInfo TryGetEnumInfoFromType(Type enumType, string attributeName, XmlTypeDescription xmlDoc)
        {
            var attrInfo = DuckTyping.GetAttributeInfo(enumType, attributeName);

            if (attrInfo == null) // the enum wasn't marked with the EnumToSql attribute
                return null;

            var values = GetEnumValues(enumType, xmlDoc);
            return new EnumInfo(enumType, attrInfo, values);
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
                var displayName = field.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName ?? name;
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

                values[i] = new EnumValue(id, name, displayName, isActive, description);
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
    }
}