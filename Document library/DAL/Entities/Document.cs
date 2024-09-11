namespace Document_library.DAL.Entities
{
    public class Document
    {
        public int Id { get; set; }
        public string Type { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string Path { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public DateTime LastUpdatedAt { get; set; }

        //Navigation properties
        public User User { get; set; } = null!;
    }
}
