using System.Reflection;

namespace EnumsToSql
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