using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Text;
using System.Security.Cryptography;
using System.Collections.Concurrent;
public class SignalingServer {
    
    int port = 4567;
    int backlog = 10;
    string upperAlph = "ABCDEFGHIJKLMNOPQRSTUVQXYZ";

    ConcurrentDictionary<string, IPEndPoint> dict = new ConcurrentDictionary<string, IPEndPoint>();
    int roomCodeLength = 10;
    int serVersion = 1;
    IPEndPoint? endPoint;
    public const byte failStatus = 0x00;
    public const byte succStatus = 0xFF;

    List<Socket> clientSocks = new List<Socket>();    
    public static byte[] bytesFromInt(int num) {
        return (BitConverter.IsLittleEndian) ? BitConverter.GetBytes(num).Reverse().ToArray() : BitConverter.GetBytes(num);
    }
    public async Task Start(bool local) {
        if (endPoint == null) {
            // endPoint = new (), port);
            endPoint = new (IPAddress.Any, port);
        }
        Console.WriteLine($"IP is {endPoint.Address.ToString()} and port is {endPoint.Port}");
        await OpenServer(); 
    }
    private async Task OpenServer() {
        using Socket listener = new(
            endPoint.AddressFamily,
            SocketType.Stream,
            ProtocolType.Tcp);
        listener.Bind(endPoint);
        listener.Listen(backlog);
        while (true) {
            Socket handler = await listener.AcceptAsync();
            Console.WriteLine($"connected to {((IPEndPoint)handler.RemoteEndPoint).Address.ToString()} and port is {((IPEndPoint)handler.RemoteEndPoint).Port.ToString()}");
            ThreadPool.QueueUserWorkItem(HandleConnectionAsync, handler);
        }
        
        //TODO: process multiple servers
        
    }

    private async void HandleConnectionAsync(object? obj) {
        if (obj == null) {
            return;
        }
        Socket handler = (Socket)obj;
        bool running = true;

        while (running){
            byte[] headBuf = new byte[8];
            int received = await handler.ReceiveAsync(headBuf, SocketFlags.None);
            
            if (received != 8) {
                running = false;
                continue;
            }

            int version = Int32.Parse(Encoding.UTF8.GetString(headBuf, 0, 4));
            // int length = Int32.Parse(Encoding.UTF8.GetString(headBuf, 4, 4));
            int mType = Int32.Parse(Encoding.UTF8.GetString(headBuf, 4, 4));


            if (version >= serVersion) {
                Console.WriteLine($"client version {version} ahead of server version {serVersion}.");
                await SendMessageAsync(handler, [], 5);
                running = false;
                continue;
            }

            switch (mType) {
                case 0:
                    bool newCode = false;
                    string code = "";
                    while (!newCode) {
                        code = new string(RandomNumberGenerator.GetItems(upperAlph.AsSpan(), roomCodeLength));
                        if (!dict.ContainsKey(code)) {
                            if (handler.RemoteEndPoint != null)
                            dict.TryAdd(code, (IPEndPoint)handler.RemoteEndPoint);
                            newCode = true;
                        }
                    }
                    await SendMessageAsync(handler, code.Length + code, 1);
                    break;

                case 2:
                    byte[] len = new byte[4];
                    int recd = await handler.ReceiveAsync(len, SocketFlags.None);
                    if (recd < 4) {
                        Console.WriteLine($"expected at least 4 bytes got {recd}");
                        break;
                    }
                    byte[] message = new byte[Int32.Parse(len)];
                    recd = await handler.ReceiveAsync(message, SocketFlags.None);
                    string playerCode = Encoding.UTF8.GetString(message);
                    IPEndPoint end;
                    if (!dict.TryGetValue(playerCode, out end)) {
                        byte [] failStatus = BitConverter.GetBytes(1);
                        await SendMessageAsync(handler, failStatus, 3);
                        break;
                    }
                    await SendIPAsync(handler, end, 3, [succStatus]);
                    await SendIPAsync(handler, (IPEndPoint)handler.RemoteEndPoint, 4);
                    dict.TryRemove(playerCode, out _);
                    running = false;
                    //send code found

                    break;
                
                default:
                    Console.WriteLine($"mtype {mType} is not supported");
                    break;
            }
        }
        handler.Shutdown(SocketShutdown.Both);
        handler.Close();
    }
    private async Task SendIPAsync(Socket handler, IPEndPoint endPoint, int messCode, 
                                                        byte[]? status = null) {
        
        byte[] ip = endPoint.Address.GetAddressBytes();
        byte[] port = BitConverter.GetBytes(endPoint.Port);
        if (BitConverter.IsLittleEndian) {
            Array.Reverse(port);
        }
        byte[] message = [];
        if (status != null) {
            message = status;
        }
        await SendMessageAsync(handler, message.Concat(ip.Concat(port)).ToArray(), messCode);
    }
    private async Task SendMessageAsync(Socket handler, string message, int messCode) {
        byte[] body = Encoding.UTF8.GetBytes(message); 
        await SendMessageAsync(handler, body, messCode);
    }

    private async Task SendMessageAsync(Socket handler, byte[] message, int messCode) {
        string header = Pad(serVersion) + Pad(messCode);
        await handler.SendAsync(Encoding.UTF8.GetBytes(header).Concat(message).ToArray());
    }
    private string Pad (int num) {
        string res = "";
        for (int i = 0; i < (4-num.ToString().Length); i++) {
            res += "0";
        }
        return res + num.ToString();
    }

    static async Task<string> GetPublicIpAsync
    (string serviceUrl = "https://api.ipify.org/") {
        using (HttpClient client = new HttpClient()) {
            return await client.GetStringAsync(serviceUrl);
        // return System.Net.IPAddress.Parse(sIPAddress);
        }
    }
}
