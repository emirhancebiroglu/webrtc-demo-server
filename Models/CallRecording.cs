using System.ComponentModel.DataAnnotations;

namespace WebRTCWebSocketServer.Models
{
    public class CallRecording
    {
        [Required]
        [Key]
        public required string CallId { get; set; }

        public DateTime Timestamp { get; set; }

        public List<RecordingFile> RecordingFiles { get; set; } = [];
    }
}