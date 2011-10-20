using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Fleck.Handlers
{
    public static class Draft76Handler
    {
        private const byte End = 255;
        private const byte Start = 0;
        private const int MaxSize = 1024 * 1024 * 5;
        private static readonly MD5 Md5 = MD5.Create();
        
        public static IHandler Create(WebSocketHttpRequest request, Action<string> onMessage)
        {
            return new ComposableHandler
            {
                Frame = Draft76Handler.FrameText,
                Handshake = () => Draft76Handler.Handshake(request),
                ReceiveData = data => ReceiveData(onMessage, data)
            };
        }
        
        public static void ReceiveData(Action<string> onMessage, List<byte> data)
        {
            while (data.Count > 0)
            {
                if (data[0] != Start)
                    throw new WebSocketException("Invalid Frame");
                
                var endIndex = data.IndexOf(End);
                if (endIndex < 0)
                    return;
                
                if (endIndex > MaxSize)
                    throw new WebSocketException("Frame too large");
                
                var bytes = data.Skip(1).Take(endIndex - 1).ToArray();
                
                data.RemoveRange(0, endIndex + 1);
                
                var message = Encoding.UTF8.GetString(bytes);
                
                onMessage(message);
            }
        }
        
        public static byte[] FrameText(string data)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(data);
            // wrap the array with the wrapper bytes
            var wrappedBytes = new byte[bytes.Length + 2];
            wrappedBytes[0] = Start;
            wrappedBytes[wrappedBytes.Length - 1] = End;
            Array.Copy(bytes, 0, wrappedBytes, 1, bytes.Length);
            return wrappedBytes;
        }
        
        public static byte[] Handshake(WebSocketHttpRequest request)
        {
            FleckLog.Debug("Building Draft76 Response");
            
            var builder = new StringBuilder();
            builder.Append("HTTP/1.1 101 WebSocket Protocol Handshake\r\n");
            builder.Append("Upgrade: WebSocket\r\n");
            builder.Append("Connection: Upgrade\r\n");
            builder.AppendFormat("Sec-WebSocket-Origin: {0}\r\n",  request["Origin"]);
            builder.AppendFormat("Sec-WebSocket-Location: {0}://{1}{2}\r\n", request.Scheme, request["Host"], request.Path);

            if (request.Headers.ContainsKey("Sec-WebSocket-Protocol"))
                builder.AppendFormat("Sec-WebSocket-Protocol: {0}\r\n", request["Sec-WebSocket-Protocol"]);
                
            builder.Append("\r\n");
            
            var key1 = request["Sec-WebSocket-Key1"]; 
            var key2 = request["Sec-WebSocket-Key2"]; 
            var challenge = new ArraySegment<byte>(request.Bytes, request.Bytes.Length - 8, 8);
            
            var answerBytes = CalculateAnswerBytes(key1, key2, challenge);

            byte[] byteResponse = Encoding.ASCII.GetBytes(builder.ToString());
            int byteResponseLength = byteResponse.Length;
            Array.Resize(ref byteResponse, byteResponseLength + answerBytes.Length);
            Array.Copy(answerBytes, 0, byteResponse, byteResponseLength, answerBytes.Length);
            
            return byteResponse;
        }
        
        public static byte[] CalculateAnswerBytes(string key1, string key2, ArraySegment<byte> challenge)
        {
            byte[] result1Bytes = ParseKey(key1);
            byte[] result2Bytes = ParseKey(key2);

            var rawAnswer = new byte[16];
            Array.Copy(result1Bytes, 0, rawAnswer, 0, 4);
            Array.Copy(result2Bytes, 0, rawAnswer, 4, 4);
            Array.Copy(challenge.Array, challenge.Offset, rawAnswer, 8, 8);

            return Md5.ComputeHash(rawAnswer);
        }

        private static byte[] ParseKey(string key)
        {
            int spaces = key.Count(x => x == ' ');
            var digits = new String(key.Where(Char.IsDigit).ToArray());

            var value = (Int32)(Int64.Parse(digits) / spaces);

            byte[] result = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(result);
            return result;
        }
    }
}