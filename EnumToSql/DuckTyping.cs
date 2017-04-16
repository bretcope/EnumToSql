using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using EnumToSql.Logging;

namespace EnumToSql
{
    // Provides duck-typing for the EnumToSql attribute
    static class DuckTyping
    {
        delegate AttributeInfo Getter(Attribute attr, Type enumType);

        static readonly Dictionary<Type, Getter> s_gettersByType = new Dictionary<Type, Getter>();

        internal static AttributeInfo GetAttributeInfo(Type enumType, string attributeName)
        {
            foreach (var data in enumType.GetCustomAttributesData())
            {
                if (data.AttributeType.Name == attributeName)
                {
                    var attr = enumType.GetCustomAttribute(data.AttributeType);
                    var attrType = attr.GetType();
                    var getter = GetGetter(attrType);

                    return getter(attr, enumType);
                }
            }

            return null;
        }

        static Getter GetGetter(Type attrType)
        {
            Getter getter;
            if (!s_gettersByType.TryGetValue(attrType, out getter))
            {
                getter = CreateGetter(attrType);
                s_gettersByType[attrType] = getter;
            }

            return getter;
        }

        static Getter CreateGetter(Type attrType)
        {
            var name = "AttributeInfoGetter_" + attrType.FullName;
            var infoType = typeof(AttributeInfo);
            var paramTypes = new[] { typeof(Attribute), typeof(Type) };
            var dm = new DynamicMethod(name, infoType, paramTypes, true);
            var il = dm.GetILGenerator();

            il.DeclareLocal(attrType);
            il.Emit(OpCodes.Ldarg_0);               // [attr]
            il.Emit(OpCodes.Castclass, attrType);   // [attr]
            il.Emit(OpCodes.Stloc_0);               // empty

            EmitSetupCall(il, attrType);

            var constructor = infoType.GetConstructor(Type.EmptyTypes);
            il.Emit(OpCodes.Newobj, constructor); // [info]

            var tableNameExists = EmitProperty(nameof(AttributeInfo.Table), "", il, attrType);
            if (!tableNameExists)
                throw new EnumsToSqlException($"{attrType.FullName} is missing required property \"{nameof(AttributeInfo.Table)}\"");

            EmitProperty(nameof(AttributeInfo.Schema), "dbo", il, attrType);
            EmitProperty(nameof(AttributeInfo.DeletionMode), nameof(DeletionMode.MarkAsInactive), il, attrType);

            EmitProperty(nameof(AttributeInfo.IdColumn), "Id", il, attrType);
            EmitProperty(nameof(AttributeInfo.IdColumnSize), 0, il, attrType);

            EmitProperty(nameof(AttributeInfo.NameColumn), "Name", il, attrType);
            EmitProperty(nameof(AttributeInfo.NameColumnSize), 250, il, attrType);
            EmitProperty(nameof(AttributeInfo.NameColumnEnabled), true, il, attrType);

            EmitProperty(nameof(AttributeInfo.DisplayNameColumn), "DisplayName", il, attrType);
            EmitProperty(nameof(AttributeInfo.DisplayNameColumnSize), 250, il, attrType);
            EmitProperty(nameof(AttributeInfo.DisplayNameColumnEnabled), true, il, attrType);

            EmitProperty(nameof(AttributeInfo.DescriptionColumn), "Description", il, attrType);
            EmitProperty(nameof(AttributeInfo.DescriptionColumnSize), int.MaxValue, il, attrType);
            EmitProperty(nameof(AttributeInfo.DescriptionColumnEnabled), true, il, attrType);

            EmitProperty(nameof(AttributeInfo.IsActiveColumn), "IsActive", il, attrType);
            EmitProperty(nameof(AttributeInfo.IsActiveColumnEnabled), true, il, attrType);

            il.Emit(OpCodes.Ret);

            return (Getter)dm.CreateDelegate(typeof(Getter));
        }

        static void EmitSetupCall(ILGenerator il, Type attrType)
        {
            var setupMethod = attrType.GetMethod(
                "Setup",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { typeof(Type) },
                null);

            if (setupMethod != null)
            {
                if (setupMethod.ReturnType != typeof(void))
                    throw new EnumsToSqlException($"Setup method does not return void. Attribute: {attrType.FullName}");

                il.Emit(OpCodes.Ldloc_0);               // [attr]
                il.Emit(OpCodes.Ldarg_1);               // [attr] [enumType]
                il.Emit(OpCodes.Callvirt, setupMethod); // empty
            }
        }

        static bool EmitProperty(string name, object defaultValue, ILGenerator il, Type attrType)
        {
            // Initial stack: [info]

            const BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var attrProp = attrType.GetProperty(name, bindingFlags);
            var infoProp = typeof(AttributeInfo).GetProperty(name, bindingFlags);

            var propType = defaultValue.GetType();

            Debug.Assert(infoProp.PropertyType == propType);

            il.Emit(OpCodes.Dup); // [info] [info]

            var propExists = attrProp?.GetMethod != null;

            if (propExists)
            {
                if (attrProp.PropertyType != propType)
                    throw new Exception($"{attrType.FullName} has a property named {name}, but it is not of type {propType.Name}");

                il.Emit(OpCodes.Ldloc_0);                      // [info] [info] [attr]
                il.Emit(OpCodes.Callvirt, attrProp.GetMethod); // [info] [info] [value]
            }
            else if (propType == typeof(string))
            {
                il.Emit(OpCodes.Ldstr, (string)defaultValue); // [info] [info] [value]
            }
            else if (propType == typeof(int))
            {
                il.Emit(OpCodes.Ldc_I4, (int)defaultValue); // [info] [info] [value]
            }
            else if (propType == typeof(bool))
            {
                var op = (bool)defaultValue ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0;
                il.Emit(op); // [info] [info] [value]
            }
            else
            {
                throw new NotImplementedException($"Unexpected property type {propType.Name}. This is a bug in EnumToSql.");
            }

            il.Emit(OpCodes.Call, infoProp.SetMethod);     // [info]

            return propExists;
        }
    }
}