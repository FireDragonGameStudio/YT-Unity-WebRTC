using UnityEngine;
using WebSocketSharp;
using WebSocketSharp.Server;

public class MultiReceiverMediaChannelService : WebSocketBehavior {
    private static int channelCount = 0;
    private static string serverId = "";

    // works when sender connects to server first

    protected override void OnMessage(MessageEventArgs e) {
        Debug.Log(ID + " - MultiReceiverMediaChannel SERVER got message " + e.Data);

        // check if server is connecting
        if (e.Data.Equals("SENDER")) {
            serverId = ID;
        } else if (e.Data.Equals("RECEIVER")) {
            Sessions.SendTo("CHANNEL!" + channelCount + "!New receiver added.", serverId);
            Sessions.SendTo("CHANNEL!" + channelCount + "!New receiver connected.", ID);
            channelCount++;
        } else {
            foreach (var id in Sessions.ActiveIDs) {
                if (id != ID) {
                    Sessions.SendTo(e.Data, id);
                }
            }
        }
    }
}
