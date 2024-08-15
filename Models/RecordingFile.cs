using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebRTCWebSocketServer.Models
{
    public class RecordingFile
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public required string FilePath { get; set; }

        [Required]
        public required string FileType { get; set; }

        [Required]
        [ForeignKey(nameof(CallRecording))]
        public required string CallId { get; set; }

        public CallRecording? CallRecording { get; set; }
    }
}