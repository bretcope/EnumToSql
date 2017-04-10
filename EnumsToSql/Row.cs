namespace EnumsToSql
{
    class Row
    {
        public long Id { get; }
        public string Name { get; }
        public string Description { get; }
        public bool IsActive { get; }

        internal Row(long id, string name, string desc, bool isActive)
        {
            Id = id;
            Name = name;
            Description = desc;
            IsActive = isActive;
        }
    }
}