using System.Net.WebSockets;
using System.Text;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using WebRTCWebSocketServer.Models;
using Microsoft.EntityFrameworkCore;
using System.Dynamic;

namespace WebRTCWebSocketServer.Handlers
{
    public class WebSocketHandler(ApplicationDbContext context)
    {
        private static readonly List<WebSocket> _sockets = [];
        private readonly ApplicationDbContext _context = context;
        private string? CallId = null;

        public async Task HandleWebSocketConnection(WebSocket webSocket)
        {
            _sockets.Add(webSocket);

            var buffer = new byte[1024 * 4];
            var messageBuilder = new StringBuilder();

            WebSocketReceiveResult result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

            while (!result.CloseStatus.HasValue)
            {
                if(result.MessageType == WebSocketMessageType.Text){
                    var messagePart = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    messageBuilder.Append(messagePart);

                    if (result.EndOfMessage)
                    {
                        try
                        {
                            var message = messageBuilder.ToString();
                            var jsonMessage = JObject.Parse(message);

                            SendSDPAndCandidatesToClient(jsonMessage, message, result, webSocket);

                            if (jsonMessage.TryGetValue("type", out JToken? typeToken))
                            {
                                string messageType = typeToken.ToString();

                                if (CallId == null)
                                {
                                    CallId = jsonMessage.TryGetValue("id", out JToken? idToken) ? idToken.ToString() : null;
                                }
                                else
                                {
                                    System.Console.WriteLine("CallId: " + CallId);
                                }

                                HandleVideoData(jsonMessage, messageType);
                                HandleAudioData(jsonMessage, messageType);
                                await HandleHangupAsync(messageType, message, webSocket);
                                HandleCallId(messageType, message, webSocket);
                                
                            }
                            else
                            {
                                Console.WriteLine("No message type found.");
                            }
                        }
                        catch (JsonReaderException)
                        {
                            Console.WriteLine("Received malformed JSON message.");
                        }

                        messageBuilder.Clear();
                    }
                }

                result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            }

            _sockets.Remove(webSocket);
            await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
        }

        private async Task<CallRecording> GetOrCreateCallRecordingAsync(string callId)
        {
            var callRecording = await _context.CallRecordings
                .FirstOrDefaultAsync(c => c.CallId == callId);

            if (callRecording == null)
            {
                callRecording = new CallRecording
                {
                    CallId = callId,
                    Timestamp = DateTime.UtcNow
                };
                _context.CallRecordings.Add(callRecording);
                await _context.SaveChangesAsync();
            }

            return callRecording;
        }

        private void SaveRecordingFile(byte[] data, string fileType)
        {
            string rootDirectory = Directory.GetCurrentDirectory();
            string directory = fileType.Contains("Video") ? "Video" : "Audio";
            string fileDirectory = Path.Combine(rootDirectory, directory);

            if (!Directory.Exists(fileDirectory))
            {
                Directory.CreateDirectory(fileDirectory);
            }

            string filePath = Path.Combine(fileDirectory, $"{CallId}-{fileType}.webm");

            try
            {
                using var fileStream = new FileStream(filePath, FileMode.Append, FileAccess.Write);
                fileStream.Write(data, 0, data.Length);
                Console.WriteLine($"{fileType} data saved to file.");

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving {fileType} data: {ex.Message}");
            }
        }

        private static async void SendSDPAndCandidatesToClient(JObject jsonMessage, string message, WebSocketReceiveResult result, WebSocket webSocket){
            bool isRelevantMessage = jsonMessage.ContainsKey("offer") || 
            jsonMessage.ContainsKey("answer") || 
            jsonMessage.ContainsKey("candidate");

            if (isRelevantMessage)
            {
                foreach (var socket in _sockets)
                {
                    if (socket != webSocket && socket.State == WebSocketState.Open)
                    {
                        var encodedMessage = Encoding.UTF8.GetBytes(message);
                        await socket.SendAsync(new ArraySegment<byte>(encodedMessage, 0, encodedMessage.Length), result.MessageType, result.EndOfMessage, CancellationToken.None);
                    }
                }
            }
        }

        private void HandleVideoData(JObject jsonMessage, string messageType)
        {
            if (jsonMessage.TryGetValue("data", out JToken? dataToken))
            {
                string base64Data = dataToken.ToString();
                byte[] videoData = Convert.FromBase64String(base64Data);

                if (messageType == "callerVideo" || messageType == "calleeVideo")
                {
                    SaveRecordingFile(videoData, messageType);
                }
                else
                {
                    Console.WriteLine($"Unknown video type: {messageType}");
                }
            }
            else
            {
                Console.WriteLine("No video data found.");
            }
        }

        private void HandleAudioData(JObject jsonMessage, string messageType)
        {
            if (jsonMessage.TryGetValue("data", out JToken? dataToken))
            {
                string base64Data = dataToken.ToString();
                byte[] audioData = Convert.FromBase64String(base64Data);

                if (messageType == "callerAudio" || messageType == "calleeAudio")
                {
                    SaveRecordingFile(audioData, messageType);
                }
                else
                {
                    Console.WriteLine($"Unknown audio type: {messageType}");
                }
            }
            else
            {
                Console.WriteLine("No audio data found.");
            }
        }

        private async Task HandleHangupAsync(string messageType, string message, WebSocket webSocket)
        {
            if (messageType == "hangup")
            {
                foreach (var socket in _sockets)
                {
                    if (socket != webSocket && socket.State == WebSocketState.Open)
                    {
                        var encodedMessage = Encoding.UTF8.GetBytes(message);
                        await socket.SendAsync(new ArraySegment<byte>(encodedMessage, 0, encodedMessage.Length), WebSocketMessageType.Text, true, CancellationToken.None);
                    }
                }

                System.Console.WriteLine("call id: " + CallId);

                if (CallId != null)
                {
                    await SaveAllRecordingFilesAsync();
                    CallId = null;
                }
                else
                {
                    Console.WriteLine("No call ID found in the hangup message.");
                }
            }
        }

        private async Task SaveAllRecordingFilesAsync()
        {
            if (CallId != null)
            {
                var callRecording = await GetOrCreateCallRecordingAsync(CallId);

                string rootDirectory = Directory.GetCurrentDirectory();
                string audioDirectory = Path.Combine(rootDirectory, "Audio");
                string videoDirectory = Path.Combine(rootDirectory, "Video");

                var videoFiles = Directory.GetFiles(videoDirectory, $"{CallId}-callerVideo.webm")
                    .Concat(Directory.GetFiles(videoDirectory, $"{CallId}-calleeVideo.webm"))
                    .ToList();
                var audioFiles = Directory.GetFiles(audioDirectory, $"{CallId}-callerAudio.webm")
                    .Concat(Directory.GetFiles(audioDirectory, $"{CallId}-calleeAudio.webm"))
                    .ToList();

                // Check if there are any files to process
                if (videoFiles.Count == 0 && audioFiles.Count == 0)
                {
                    Console.WriteLine("No recording files found.");
                    return;
                }

                foreach (var filePath in videoFiles.Concat(audioFiles))
                {
                    System.Console.WriteLine("Processing file: " + filePath);
                    string fileType = filePath.Contains("Video") ? "Video" : "Audio";
                    
                    var recordingFile = new RecordingFile
                    {
                        FilePath = filePath,
                        FileType = fileType,
                        CallId = callRecording.CallId
                    };

                    System.Console.WriteLine("Recording file: " + recordingFile);

                    _context.RecordingFiles.Add(recordingFile);
                }

                await _context.SaveChangesAsync();
                Console.WriteLine("All recording files saved to the database.");
            }
        }

        private static async void HandleCallId(string messageType, string message, WebSocket webSocket)
        {
            if (messageType == "callId")
            {
                foreach (var socket in _sockets)
                {
                    if (socket != webSocket && socket.State == WebSocketState.Open)
                    {
                        var encodedMessage = Encoding.UTF8.GetBytes(message);
                        await socket.SendAsync(new ArraySegment<byte>(encodedMessage, 0, encodedMessage.Length), WebSocketMessageType.Text, true, CancellationToken.None);
                    }
                }
            }
        }
    }
}