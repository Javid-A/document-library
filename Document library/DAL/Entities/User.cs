using Microsoft.AspNetCore.Identity;

namespace Document_library.DAL.Entities
{
    public class User:IdentityUser
    {
        //Navigation properties
        public ICollection<Document> Documents{ get; set; }
        public User()
        {
            Documents = [];
        }
    }
}
