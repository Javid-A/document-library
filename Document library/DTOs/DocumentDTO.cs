namespace Document_library.DTOs
{
    public class DocumentDTO
    {
        public string Type { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string Path { get; set; } = null!;
        public int Downloads { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
