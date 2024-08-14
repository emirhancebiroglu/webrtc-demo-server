using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebRTCWebSocketServer.Models
{
    public class RecordingFile
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string FilePath { get; set; }

        [Required]
        public string FileType { get; set; }

        [Required]
        public int CallRecordingId { get; set; }

        public CallRecording CallRecording { get; set; }
    }
}