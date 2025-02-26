using UnityEngine;
using WebSocketSharp.Server;

public class SimpleServer : MonoBehaviour {
    private WebSocketServer wssv;
    private string serverIpv4Address;
    private int serverPort = 8080;

    private void Awake() {
        wssv = new WebSocketServer($"ws://{serverIpv4Address}:{serverPort}");

        wssv.AddWebSocketService<SimpleService>($"/{nameof(SimpleService)}");

        wssv.Start();
    }

    private void OnDestroy() {
        wssv.Stop();
    }
}
