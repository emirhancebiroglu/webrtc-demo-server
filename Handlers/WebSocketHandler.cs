using System.Net.WebSockets;
using System.Text;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using WebRTCWebSocketServer.Models;

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
                                string callId = jsonMessage.TryGetValue("id", out JToken? idToken) ? idToken.ToString() : string.Empty;

                                HandleVideoData(jsonMessage, messageType, callId);
                                HandleAudioData(jsonMessage, messageType, callId);
                                HandleHangup(messageType, message, webSocket);
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

        private void SaveCallerVideo(byte[] data, string callId){
            string rootDirectory = Directory.GetCurrentDirectory();
            string videoDirectory = Path.Combine(rootDirectory, "Video");

            if (!Directory.Exists(videoDirectory))
            {
                Directory.CreateDirectory(videoDirectory);
            }

            string callerVideoFilePath = Path.Combine(videoDirectory, $"{callId}-caller.webm");

            try
            {
                using var fileStream = new FileStream(callerVideoFilePath, FileMode.Append, FileAccess.Write);
                fileStream.Write(data, 0, data.Length);

                Console.WriteLine("Caller video data appended to file.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving caller video data to file: {ex.Message}");
            }
        }

        private void SaveCallerAudio(byte[] data, string callId){
            string rootDirectory = Directory.GetCurrentDirectory();
            string audioDirectory = Path.Combine(rootDirectory, "Audio");

            if (!Directory.Exists(audioDirectory))
            {
                Directory.CreateDirectory(audioDirectory);
            }

            string callerAudioFilePath = Path.Combine(audioDirectory, $"{callId}-caller.webm");

            try
            {
                using var fileStream = new FileStream(callerAudioFilePath, FileMode.Append, FileAccess.Write);
                fileStream.Write(data, 0, data.Length);

                Console.WriteLine("Caller video data appended to file.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving caller video data to file: {ex.Message}");
            }
        }

        private void SaveCalleeVideo(byte[] data, string callId){
            string rootDirectory = Directory.GetCurrentDirectory();
            string videoDirectory = Path.Combine(rootDirectory, "Video");

            if (!Directory.Exists(videoDirectory))
            {
                Directory.CreateDirectory(videoDirectory);
            }

            string calleeVideoFilePath = Path.Combine(videoDirectory, $"{callId}-callee.webm");

            try
            {
                using var fileStream = new FileStream(calleeVideoFilePath, FileMode.Append, FileAccess.Write);
                fileStream.Write(data, 0, data.Length);

                Console.WriteLine("Callee video data appended to file.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving callee video data to file: {ex.Message}");
            }
        }

        private void SaveCalleeAudio(byte[] data, string callId){
            string rootDirectory = Directory.GetCurrentDirectory();
            string audioDirectory = Path.Combine(rootDirectory, "Audio");

            if (!Directory.Exists(audioDirectory))
            {
                Directory.CreateDirectory(audioDirectory);
            }

            string calleeAudioFilePath = Path.Combine(audioDirectory, $"{callId}-callee.webm");

            try
            {
                using var fileStream = new FileStream(calleeAudioFilePath, FileMode.Append, FileAccess.Write);
                fileStream.Write(data, 0, data.Length);

                Console.WriteLine("Callee video data appended to file.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving callee video data to file: {ex.Message}");
            }
        }

        private async void SendSDPAndCandidatesToClient(JObject jsonMessage, string message, WebSocketReceiveResult result, WebSocket webSocket){
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

        private void HandleVideoData(JObject jsonMessage, string messageType, string callId){
            if (jsonMessage.TryGetValue("data", out JToken? dataToken))
            {
                string base64Data = dataToken.ToString();
                byte[] videoData = Convert.FromBase64String(base64Data);

                if (messageType == "callerVideo")
                {
                    SaveCallerVideo(videoData, callId);
                }
                else if (messageType == "calleeVideo")
                                    {
                    SaveCalleeVideo(videoData, callId);
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

        private void HandleAudioData(JObject jsonMessage, string messageType, string callId){
            if (jsonMessage.TryGetValue("data", out JToken? dataToken))
            {
                string base64Data = dataToken.ToString();
                byte[] videoData = Convert.FromBase64String(base64Data);

                if (messageType == "callerAudio")
                {
                    SaveCallerAudio(videoData, callId);
                }
                else if (messageType == "calleeAudio")
                                    {
                    SaveCalleeAudio(videoData, callId);
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

        private async void HandleHangup(string messageType, string message, WebSocket webSocket)
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
            }
        }

        private async void HandleCallId(string messageType, string message, WebSocket webSocket)
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