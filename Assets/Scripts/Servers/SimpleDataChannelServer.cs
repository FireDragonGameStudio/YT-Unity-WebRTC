using System.Net;
using System.Net.Sockets;
using UnityEngine;
using WebSocketSharp.Server;

public class SimpleDataChannelServer : MonoBehaviour {
    private WebSocketServer wssv;
    private string serverIpv4Address;
    private int serverPort = 8080;

    private void Awake() {
        // get server ip in network
        var host = Dns.GetHostEntry(Dns.GetHostName());
        foreach (var ip in host.AddressList) {
            if (ip.AddressFamily == AddressFamily.InterNetwork) {
                serverIpv4Address = ip.ToString();
                break;
            }
        }

        wssv = new WebSocketServer($"ws://{serverIpv4Address}:{serverPort}");

        wssv.AddWebSocketService<SimpleDataChannelService>($"/{nameof(SimpleDataChannelService)}");
        //wssv.AddWebSocketService<MultiReceiverMediaChannelService>($"/{nameof(MultiReceiverMediaChannelService)}");
        //wssv.AddWebSocketService<VideoChatMediaStreamService>($"/{nameof(VideoChatMediaStreamService)}");

        wssv.Start();
    }

    private void OnDestroy() {
        wssv.Stop();
    }
}
