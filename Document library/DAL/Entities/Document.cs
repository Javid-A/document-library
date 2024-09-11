namespace Document_library.DAL.Entities
{
    public class Document:BaseEntity
    {
        public string Type { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string Path { get; set; } = null!;
        //Navigation properties
        public string UserId { get; set; }
        public User User { get; set; } = null!;
    }
}
