using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
public class SignalingServer {
    public async Task Start() {
        Console.WriteLine(await GetPublicIpAsync());
    }
    static async Task<string> GetPublicIpAsync
    (string serviceUrl = "https://api.ipify.org/") {
    using (HttpClient client = new HttpClient()) {
        return await client.GetStringAsync(serviceUrl);
        // return System.Net.IPAddress.Parse(sIPAddress);
    }
    
}
}
