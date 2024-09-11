using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Document_library.DAL
{
    public class DocumentDB(DbContextOptions<DocumentDB> opt) : IdentityDbContext(opt)
    {
    }   
}
