using System.Collections.Generic;
using System.Text;
using System.Xml;

namespace EnumsToSql
{
    class XmlAssemblyDocument
    {
        public string Name { get; }
        public Dictionary<string, XmlTypeDescription> TypesDescriptions { get; }

        XmlAssemblyDocument(string name, Dictionary<string, XmlTypeDescription> typesDescriptions)
        {
            Name = name;
            TypesDescriptions = typesDescriptions;
        }

        internal static XmlAssemblyDocument GetFromFile(string fileName)
        {
            var xml = new XmlDocument();
            xml.Load(fileName);
            return GetFromXmlDocument(xml);
        }

        static XmlAssemblyDocument GetFromXmlDocument(XmlDocument xml)
        {
            var name = xml.SelectSingleNode("/doc/assembly/name")?.InnerText;

            var membersNode = xml.SelectSingleNode("/doc/members");
            var types = ReadTypesFromMembersXml(membersNode);

            return new XmlAssemblyDocument(name, types);
        }

        static Dictionary<string, XmlTypeDescription> ReadTypesFromMembersXml(XmlNode membersNode)
        {
            var types = new Dictionary<string, XmlTypeDescription>();

            if (membersNode == null)
                return types;

            var sb = new StringBuilder();
            var children = membersNode.ChildNodes;
            var count = children.Count;
            for (var i = 0; i < count; i++)
            {
                var child = children[i];
                if (child.NodeType != XmlNodeType.Element || child.Name != "member")
                    continue;

                var name = child.Attributes?.GetNamedItem("name")?.Value;
                if (name == null || name[0] != 'T' && name[0] != 'F') // we only care about enums - which means types and fields
                    continue;

                var summary = GetSummary(child, sb);

                var isType = name[0] == 'T';
                string typeName, fieldName;

                if (isType)
                {
                    typeName = name.Substring(2);
                    fieldName = null;
                }
                else
                {
                    var lastDot = name.LastIndexOf('.');
                    typeName = name.Substring(2, lastDot - 2);
                    fieldName = name.Substring(lastDot + 1);
                }

                XmlTypeDescription info;
                if (!types.TryGetValue(typeName, out info))
                {
                    info = new XmlTypeDescription(typeName);
                    types[typeName] = info;
                }

                if (isType)
                {
                    info.Summary = summary;
                }
                else
                {
                    info.FieldSummaries[fieldName] = summary;
                }
            }

            return types;
        }

        static string GetSummary(XmlNode node, StringBuilder sb)
        {
            var summaryNode = node["summary"];

            if (summaryNode == null || !summaryNode.HasChildNodes)
                return "";

            var children = summaryNode.ChildNodes;
            var count = children.Count;
            for (var i = 0; i < count; i++)
            {
                var child = children[i];

                if (child.NodeType == XmlNodeType.Text)
                {
                    sb.Append(child.Value);
                }
                else if (child.NodeType == XmlNodeType.Element)
                {
                    if (child.Name == "see")
                    {
                        var cref = child.Attributes?.GetNamedItem("cref")?.Value;
                        cref = GetBasicName(cref);
                        sb.Append(cref);
                    }
                    else
                    {
                        sb.Append(child.InnerText);
                    }
                }
            }

            var summary = TrimSummaryBody(sb);
            sb.Clear();
            return summary;
        }

        static string GetBasicName(string name)
        {
            if (name == null || name.Length < 3 || name[1] != ':')
                return name;

            int index;
            switch (name[0])
            {
                case 'T': // type
                    index = name.LastIndexOf('.');
                    break;
                case 'F': // field
                case 'P': // property
                case 'M': // method
                case 'E': // event
                    index = name.LastIndexOf('.');
                    if (index > 0)
                        index = name.LastIndexOf('.', index - 1);
                    break;
                case 'N': // namespace
                default:
                    index = 1;
                    break;
            }

            if (index < 0)
                index = 1;

            return name.Substring(index + 1).Replace('#', '.');
        }

        static string TrimSummaryBody(StringBuilder sb)
        {
            sb.Replace("\n            ", "\n");

            for (var i = 0; i < sb.Length; i++)
            {
                if (sb[i] != '\n')
                    continue;

                var hasCr = i > 0 && sb[i - 1] == '\r';
                var newLineStart = hasCr ? i - 1 : i;

                var nonWhiteIndex = NextNonWhiteSpaceCharIndex(sb, i + 1);

                if (nonWhiteIndex == -1)
                {
                    // nothing left but whitespace - we should truncate it
                    sb.Remove(newLineStart, sb.Length - newLineStart);
                    break;
                }

                var nonWhiteChar = sb[nonWhiteIndex];

                if (nonWhiteChar == '\r' && nonWhiteIndex + 1 < sb.Length && sb[nonWhiteIndex + 1] == '\n')
                {
                    nonWhiteIndex++;
                    nonWhiteChar = '\n';
                }

                // if the new line is immediately followed by another new line, then we want to keep them both.
                if (nonWhiteChar == '\n')
                {
                    i = nonWhiteIndex;
                }
                else
                {
                    // not a double new line - we want to turn it into a space instead
                    var spaceExists = (newLineStart > 0 && sb[newLineStart - 1] == ' ') || sb[i + 1] == ' ';
                    if (!spaceExists)
                    {
                        sb[newLineStart] = ' ';
                        if (hasCr)
                        {
                            sb.Remove(i, 1);
                            i--;
                        }
                    }
                }
            }

            return sb.ToString().Trim();
        }

        static int NextNonWhiteSpaceCharIndex(StringBuilder sb, int index)
        {
            var len = sb.Length;
            for (var i = index; i < len; i++)
            {
                switch (sb[i])
                {
                    case ' ':
                    case '\t':
                        continue;
                    default:
                        return i;
                }
            }

            return -1;
        }
    }
}