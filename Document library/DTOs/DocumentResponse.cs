namespace Document_library.DTOs
{
    public class DocumentResponse
    {
        public string Name { get; set; } = null!;
        public Stream Stream { get; set; } = null!;
        public string ContentType { get; set; } = null!;
    }
}
