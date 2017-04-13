using System.Reflection;

namespace EnumToSql
{
    class AssemblyInfo
    {
        public Assembly Assembly { get; }
        public XmlAssemblyDocument XmlDocument { get; }

        public AssemblyInfo(Assembly asm, XmlAssemblyDocument xml)
        {
            Assembly = asm;
            XmlDocument = xml;
        }
    }
}