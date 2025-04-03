

class Program {

    static async Task Main() { 
        SignalingServer server = new SignalingServer();
        await server.Start();
    }
}