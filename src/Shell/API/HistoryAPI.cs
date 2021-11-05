using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Dotnet.Shell.API
{
    public class HistoryAPI
    {
        public static async Task SearchResultAsync(string term, int port, string token)
        {
            using (var client = new TcpClient(IPAddress.Loopback.ToString(), port))
            using (var sw = new StreamWriter(client.GetStream()))
            {
                await sw.WriteLineAsync(token);
                await sw.WriteLineAsync(term);
            }
        }

        public static async Task<string> ListenForSearchResultAsync(Action<int, string> onStartedListening)
        {
            var result = string.Empty;

            var r = new Random();
            var token = Guid.NewGuid().ToString();
            TcpListener listener;

            int port;
            while (true)
            {
                try
                {
                    var randomPortToTry = r.Next(1025, 65535);
                    listener = new TcpListener(IPAddress.Loopback, randomPortToTry);
                    listener.Start();
                    port = randomPortToTry;
                    break;
                }
                catch (SocketException)
                {

                }
            }

            onStartedListening?.Invoke(port, token);

            using (var client = await listener.AcceptTcpClientAsync())
            using (var sr = new StreamReader(client.GetStream()))
            {
                var clientToken = await sr.ReadLineAsync();
                if (clientToken != token)
                {
                    throw new InvalidDataException("Invalid token");
                }

                result = await sr.ReadLineAsync();
            }

            listener.Stop();
            return result;
        }
    }
}
