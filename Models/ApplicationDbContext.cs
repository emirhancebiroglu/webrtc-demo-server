using Microsoft.EntityFrameworkCore;

namespace WebRTCWebSocketServer.Models
{
    public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
    {
    }
}