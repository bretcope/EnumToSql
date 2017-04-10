using System;
using System.Collections.Generic;
using System.Reflection;

namespace EnumsToSql
{
    // Provides duck-typing for the EnumSqlTable attribute
    static class DuckTyping
    {
        public const string ATTRIBUTE_NAME = "EnumSqlTableAttribute";

        delegate EnumSqlTableAttributeInfo Getter(Attribute attr);

        static readonly Dictionary<Type, Getter> s_gettersByType = new Dictionary<Type, Getter>();

        public static EnumSqlTableAttributeInfo GetEnumSqlTableInfo(Type enumType)
        {
            foreach (var data in enumType.GetCustomAttributesData())
            {
                if (data.AttributeType.Name == ATTRIBUTE_NAME)
                {
                    var attr = enumType.GetCustomAttribute(data.AttributeType);
                    var attrType = attr.GetType();
                    var getter = GetGetter(attrType, enumType);

                    return getter(attr);
                }
            }

            return null;
        }

        static Getter GetGetter(Type attrType, Type enumType)
        {
            Getter getter;

            if (s_gettersByType.TryGetValue(attrType, out getter))
                return getter;

            const BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            var tableNameProp = attrType.GetProperty("TableName", bindingFlags);
            var schemaNameProp = attrType.GetProperty("SchemaName", bindingFlags);
            var idColumnSizeProp = attrType.GetProperty("IdColumnSize", bindingFlags);
            var idColumnNameProp = attrType.GetProperty("IdColumnName", bindingFlags);

            if (tableNameProp == null || tableNameProp.PropertyType != typeof(string))
                throw new InvalidOperationException($"Type {attrType.FullName} does not have a string \"TableName\" property.");

            if (schemaNameProp != null && schemaNameProp.PropertyType != typeof(string))
                throw new InvalidOperationException($"Type {attrType.FullName} has a property named \"SchemaName\" but it is not of type string.");

            if (idColumnSizeProp != null && idColumnSizeProp.PropertyType != typeof(int))
                throw new InvalidOperationException($"Type {attrType.FullName} has a property named \"IdColumnSize\" but it is not of type int.");

            if (idColumnNameProp != null && idColumnNameProp.PropertyType != typeof(string))
                throw new InvalidOperationException($"Type {attrType.FullName} has a property named \"IdColumnName\" but it is not of type string.");

            // if this was going to be called a ton, a dynamic method would be a better choice, but we're not all that concerned about performance
            getter = (attr) =>
            {
                var tableName = (string)tableNameProp.GetValue(attr);

                if (string.IsNullOrEmpty(tableName))
                    throw new InvalidOperationException($"TableName is null or empty. Enum: {enumType.FullName}");

                string schemaName = null;
                if (schemaNameProp != null)
                    schemaName = (string)schemaNameProp.GetValue(attr);
                
                var idColumnSize = 0;
                if (idColumnSizeProp != null)
                {
                    idColumnSize = (int)idColumnSizeProp.GetValue(attr);

                    switch(idColumnSize)
                    {
                        case 0:
                        case 1:
                        case 2:
                        case 4:
                        case 8:
                            break;
                        default:
                            throw new InvalidOperationException($"{idColumnSize} is not a valid IdColumnSize. Must be 0 (default), 1, 2, 4, or 8. Enum: {enumType.FullName}");
                    }
                }

                string idColumnName = null;
                if (idColumnNameProp != null)
                    idColumnName = (string)idColumnNameProp.GetValue(attr);

                return new EnumSqlTableAttributeInfo(tableName, schemaName, idColumnSize, idColumnName);
            };

            s_gettersByType[attrType] = getter;
            return getter;
        }
    }
}