namespace Document_library.DAL.Entities
{
    public class Document:BaseEntity
    {
        public string Type { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string Path { get; set; } = null!;
        public int Downloads { get; set; }
        public string? ThumbnailPath { get; set; }
        //Navigation properties
        public string UserId { get; set; } = null!;
        public User User { get; set; } = null!;
    }
}
