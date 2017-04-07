using System.Collections.Generic;

namespace EnumsToSql
{
    class XmlTypeDescription
    {
        public string Name { get; }
        public string Summary { get; internal set; }
        public Dictionary<string, string> FieldSummaries { get; } = new Dictionary<string, string>();

        public XmlTypeDescription(string name)
        {
            Name = name;
        }
    }
}