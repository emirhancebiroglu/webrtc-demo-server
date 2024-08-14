using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebRTCWebSocketServer.Models
{
    public class CallRecording
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string CallId { get; set; }

        public DateTime Timestamp { get; set; }

        public List<RecordingFile> RecordingFiles { get; set; } = new List<RecordingFile>();
    }
}