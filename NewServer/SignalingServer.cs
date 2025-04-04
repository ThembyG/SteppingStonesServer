using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Text;
using System.Security.Cryptography;
public class SignalingServer {
    
    int port = 4567;
    int backlog = 10;
    string upperAlph = "ABCDEFGHIJKLMNOPQRSTUVQXYZ";

    Dictionary<string, IPEndPoint> dict = new Dictionary<string, IPEndPoint>();
    int roomCodeLength = 10;
    int serVersion = 1;
    IPEndPoint? endPoint;
    byte[] failStatus = bytesFromInt(0);
    byte[] succStatus = bytesFromInt(1);
    
    public static byte[] bytesFromInt(int num) {
        return (BitConverter.IsLittleEndian) ? BitConverter.GetBytes(num).Reverse().ToArray() : BitConverter.GetBytes(num);
    }
    public async Task Start() {
        if(endPoint == null) {
            endPoint = new (IPAddress.Parse(await GetPublicIpAsync()), port);
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

        Socket handler = await listener.AcceptAsync();
        while (true){
            byte[] headBuf = new byte[8];
            int received = await handler.ReceiveAsync(headBuf, SocketFlags.None);
            
            if (received != 8) {
                continue;
            }

            int version = Int32.Parse(Encoding.UTF8.GetString(headBuf, 0, 4));
            // int length = Int32.Parse(Encoding.UTF8.GetString(headBuf, 4, 4));
            int mType = Int32.Parse(Encoding.UTF8.GetString(headBuf, 4, 4));


            if (version >= serVersion) {
                Console.WriteLine($"client version {version} ahead of server version {serVersion}.");
                continue;
            }

            //byte[] messBuf = new byte[length];

            switch (mType)
            {
                case 0:
                    bool newCode = false;
                    string code = "";
                    while (!newCode) {
                        code = new string(RandomNumberGenerator.GetItems(upperAlph.AsSpan(), roomCodeLength));
                        if (!dict.ContainsKey(code)) {
                            if (handler.RemoteEndPoint != null)
                            dict.Add(code, (IPEndPoint)handler.RemoteEndPoint);
                            newCode = true;
                        }
                    }
                    await SendMessageAsync(handler, code.Length + code, 1);
                    break;
                
                case 1:
                    Console.WriteLine($"server can't make room");
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

                    byte[] ip = end.Address.GetAddressBytes();
                    byte[] cport = BitConverter.GetBytes(end.Port);
                    if (BitConverter.IsLittleEndian) {
                        Array.Reverse(cport);
                    }
                    await SendMessageAsync(handler, succStatus.Concat(ip.Concat(cport)).ToArray(), 3);
                    //send code found

                    break;
                default:
                    Console.WriteLine($"mtype {mType} is not supported");
                    break;
            }

        }
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
