using System.Net.WebSockets;
using System.Text;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using WebRTCWebSocketServer.Models;
using Microsoft.EntityFrameworkCore;

namespace WebRTCWebSocketServer.Handlers
{
    public class WebSocketHandler(ApplicationDbContext context)
    {
        private static readonly List<WebSocket> _sockets = [];
        private readonly ApplicationDbContext _context = context;

        public async Task HandleWebSocketConnection(WebSocket webSocket)
        {
            _sockets.Add(webSocket);

            var buffer = new byte[1024 * 4];
            var messageBuilder = new StringBuilder();
            WebSocketReceiveResult? result = null;

            try
            {
                result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

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

                                    HandleVideoData(jsonMessage, messageType);
                                    HandleAudioData(jsonMessage, messageType);
                                    await HandleHangupAsync(messageType, message, webSocket, jsonMessage);
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
            }
            catch (WebSocketException ex)
            {
                Console.WriteLine($"WebSocketException: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }
            finally
            {
                _sockets.Remove(webSocket);
                if (result != null &&  result.CloseStatus != null && webSocket.State == WebSocketState.Open)
                {
                    await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
                }
                Console.WriteLine("WebSocket connection closed.");
            }
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

        private static void SaveRecordingFile(byte[] data, string fileType, string callId)
        {
            string rootDirectory = Directory.GetCurrentDirectory();
            string directory = fileType.Contains("Video") ? "Video" : "Audio";
            string fileDirectory = Path.Combine(rootDirectory, directory);

            if (!Directory.Exists(fileDirectory))
            {
                Directory.CreateDirectory(fileDirectory);
            }

            string filePath = Path.Combine(fileDirectory, $"{callId}-{fileType}.webm");

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

        private static void HandleVideoData(JObject jsonMessage, string messageType)
        {
            if (jsonMessage.TryGetValue("data", out JToken? dataToken))
            {
                string base64Data = dataToken.ToString();
                byte[] videoData = Convert.FromBase64String(base64Data);

                if (messageType == "callerVideo" || messageType == "calleeVideo")
                {
                    if (jsonMessage.TryGetValue("id", out JToken? idToken))
                    {
                        var callId = idToken.ToString();
                        SaveRecordingFile(videoData, messageType, callId);
                    }
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

        private static void HandleAudioData(JObject jsonMessage, string messageType)
        {
            if (jsonMessage.TryGetValue("data", out JToken? dataToken))
            {
                string base64Data = dataToken.ToString();
                byte[] audioData = Convert.FromBase64String(base64Data);

                if (messageType == "callerAudio" || messageType == "calleeAudio")
                {
                    if (jsonMessage.TryGetValue("id", out JToken? idToken))
                    {
                        var callId = idToken.ToString();
                        SaveRecordingFile(audioData, messageType, callId);
                    }
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

        private async Task HandleHangupAsync(string messageType, string message, WebSocket webSocket, JObject jsonMessage)
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

                if(jsonMessage.TryGetValue("callId", out JToken? callIdToken)){
                    var callId = callIdToken.ToString();
                    await SaveAllRecordingFilesAsync(callId);
                } 
            }
        }

        private async Task SaveAllRecordingFilesAsync(string callId)
        {
            if (callId != null)
            {
                var callRecording = await GetOrCreateCallRecordingAsync(callId);

                string rootDirectory = Directory.GetCurrentDirectory();
                string audioDirectory = Path.Combine(rootDirectory, "Audio");
                string videoDirectory = Path.Combine(rootDirectory, "Video");

                var videoFiles = Directory.GetFiles(videoDirectory, $"{callId}-callerVideo.webm")
                    .Concat(Directory.GetFiles(videoDirectory, $"{callId}-calleeVideo.webm"))
                    .ToList();
                var audioFiles = Directory.GetFiles(audioDirectory, $"{callId}-callerAudio.webm")
                    .Concat(Directory.GetFiles(audioDirectory, $"{callId}-calleeAudio.webm"))
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