using Microsoft.EntityFrameworkCore;

namespace WebRTCWebSocketServer.Models
{
    public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
    {
        public DbSet<CallRecording> CallRecordings { get; set; }
        public DbSet<RecordingFile> RecordingFiles { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RecordingFile>()
                .HasOne(rf => rf.CallRecording)
                .WithMany(cr => cr.RecordingFiles)
                .HasForeignKey(rf => rf.CallId);

            base.OnModelCreating(modelBuilder);
        }
    }
}