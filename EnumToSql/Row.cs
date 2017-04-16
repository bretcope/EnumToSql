namespace EnumToSql
{
    class Row
    {
        public long Id { get; }
        public string Name { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public bool IsActive { get; }

        internal Row(long id, string name, string displayName, string desc, bool isActive)
        {
            Id = id;
            Name = name;
            DisplayName = displayName;
            Description = desc;
            IsActive = isActive;
        }
    }
}