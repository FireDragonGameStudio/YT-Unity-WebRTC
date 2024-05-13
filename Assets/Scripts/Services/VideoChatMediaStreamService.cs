using System.Collections.Generic;
using WebSocketSharp;
using WebSocketSharp.Server;

public class VideoChatMediaStreamService : WebSocketBehavior {
    // naming convention is first number is sender, second number is receiver
    private static List<string> connections = new List<string>()
    {
        "01", "02", "10", "12", "20", "21"
    };
    private static int connectionCounter = 0;

    protected override void OnOpen() {
        // send clientId
        Sessions.SendTo(connectionCounter.ToString(), ID);
        connectionCounter++;

        // send all connected users
        Sessions.SendTo(string.Join("|", connections), ID);
    }

    protected override void OnMessage(MessageEventArgs e) {
        // forward messages to all other clients
        foreach (var id in Sessions.ActiveIDs) {
            if (id != ID) {
                Sessions.SendTo(e.Data, id);
            }
        }
    }
}