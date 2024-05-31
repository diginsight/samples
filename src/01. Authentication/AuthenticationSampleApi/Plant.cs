using Diginsight.Strings;

namespace SampleWebApi
{
    public class Plant
    {
        [LogStringableMember(Order = 3)]
        public Guid Id { get; set; }

        [LogStringableMember(Order = 2)]
        public string? Name { get; set; }

        [LogStringableMember(Order = 4)]
        public string? Description { get; set; }

        [LogStringableMember(Order = 5)]
        public string? Address { get; set; }

        [LogStringableMember(Order = 7)]
        public DateOnly CreationDate { get; set; }
    }
}
