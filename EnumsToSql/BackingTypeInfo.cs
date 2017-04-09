using System;

namespace EnumsToSql
{
    /// <summary>
    /// Describes informtation about an enum's backing type
    /// </summary>
    public class BackingTypeInfo
    {
        /// <summary>
        /// The backing type.
        /// </summary>
        public Type Type { get; }
        /// <summary>
        /// The size (in bytes) of the backing type.
        /// </summary>
        public int Size { get; }
        /// <summary>
        /// True if the backing type is a signed integer.
        /// </summary>
        public bool IsSigned { get; }

        BackingTypeInfo(Type type, int size, bool isSigned)
        {
            Type = type;
            Size = size;
            IsSigned = isSigned;
        }

        internal static BackingTypeInfo Get(Type enumType)
        {
            var backingType = enumType.GetEnumUnderlyingType();

            int size;
            bool signed;

            // we'd get a performance benefit by ordering these with int first, but it's unlikely to ever matter
            if (backingType == typeof(sbyte))
            {
                size = 1;
                signed = true;
            }
            else if (backingType == typeof(byte))
            {
                size = 1;
                signed = false;
            }
            else if (backingType == typeof(short))
            {
                size = 2;
                signed = true;
            }
            else if (backingType == typeof(ushort))
            {
                size = 2;
                signed = false;
            }
            else if (backingType == typeof(int))
            {
                size = 4;
                signed = true;
            }
            else if (backingType == typeof(uint))
            {
                size = 4;
                signed = false;
            }
            else if (backingType == typeof(long))
            {
                size = 8;
                signed = true;
            }
            else if (backingType == typeof(ulong))
            {
                size = 8;
                signed = false;
            }
            else
            {
                // pretty sure you'd have to do some serious assembly hacking for this to ever happen.
                throw new Exception($"Unexpected backing type ({backingType.Name}) for enum {enumType.FullName}");
            }

            return new BackingTypeInfo(backingType, size, signed);
        }
    }
}