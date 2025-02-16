using Diginsight.Stringify;

namespace AuthenticationSampleServerApi
{
    public class User
    {
        [StringifiableMember(Order = 3)]
        public Guid Id { get; set; }

        [StringifiableMember(Order = 2)]
        public string? Name { get; set; }

        [StringifiableMember(Order = 4)]
        public string? Surname { get; set; }

        [StringifiableMember(Order = 5)]
        public string? Email { get; set; }

        [StringifiableMember(Order = 7)]
        public DateOnly CreationDate { get; set; }
    }
}
