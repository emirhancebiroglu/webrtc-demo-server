using Microsoft.EntityFrameworkCore;

namespace WebRTCWebSocketServer.Models
{
    public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
    {
        public DbSet<CallRecording> CallRecordings { get; set; }
        public DbSet<RecordingFile> RecordingFiles { get; set; }
    }
}