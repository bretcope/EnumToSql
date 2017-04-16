using System.Diagnostics.CodeAnalysis;

namespace EnumToSql
{
    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Local", Justification = "Properties are set via a dynamic method.")]
    class AttributeInfo
    {
        public string Schema { get; private set; }
        public string Table { get; private set; }
        public string DeletionMode { get; private set; }
        public string IdColumn { get; private set; }
        public int IdColumnSize { get; private set; }
        public string NameColumn { get; private set; }
        public int NameColumnSize { get; private set; }
        public bool NameColumnEnabled { get; private set; }
        public string DisplayNameColumn { get; private set; }
        public int DisplayNameColumnSize { get; private set; }
        public bool DisplayNameColumnEnabled { get; private set; }
        public string DescriptionColumn { get; private set; }
        public int DescriptionColumnSize { get; private set; }
        public bool DescriptionColumnEnabled { get; private set; }
        public string IsActiveColumn { get; private set; }
        public bool IsActiveColumnEnabled { get; private set; }
    }
}