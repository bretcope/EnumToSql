namespace EnumsToSql
{
    /// <summary>
    /// Describes information about an individual enum value.
    /// </summary>
    public class EnumValue
    {
        /// <summary>
        /// The maximum length of an enum value's name. Names longer than this will trigger a failure.
        /// </summary>
        public const int MAX_NAME_LENGTH = 250;

        /// <summary>
        /// The actual value of the enum. This will be an integer primitive.
        /// </summary>
        public object Id { get; }
        /// <summary>
        /// The name of the enum value (as specified in code).
        /// </summary>
        public string Name { get; }
        /// <summary>
        /// True if the value is not deprecated (marked with the Obsolete attribute).
        /// </summary>
        public bool IsActive { get; }
        /// <summary>
        /// The description of the value (if available). This is pulled from XML summary comments, or the Description attribute (in that order).
        /// </summary>
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