namespace EnumsToSql
{
    public class EnumValue
    {
        public object Id { get; }
        public string Name { get; }
        public bool IsActive { get; }
        public string Description { get; }

        internal EnumValue(object id, string name, bool isActive, string description)
        {
            Id = id;
            Name = name;
            IsActive = isActive;
            Description = description;
        }
    }
}