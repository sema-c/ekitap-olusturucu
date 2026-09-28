using Microsoft.EntityFrameworkCore;
using EKitapAPI.Models;

namespace EKitapAPI.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Kitap> Kitaplar { get; set; }
        public DbSet<Bildiri> Bildiriler { get; set; }
    }
}