using NativeWebSocket;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Unity.WebRTC;
using UnityEngine;

public class STUNDataChannelReceiver : MonoBehaviour {
    private RTCPeerConnection connection;
    private RTCDataChannel dataChannel;

    private WebSocket ws;
    private string clientId;

    private bool hasReceivedOffer = false;
    private SessionDescription receivedOfferSessionDescTemp;

    private void Start() {
        InitClient("unity-stun-signaling.glitch.me");
    }

    private void Update() {
        if (hasReceivedOffer) {
            hasReceivedOffer = !hasReceivedOffer;
            StartCoroutine(CreateAnswer());
        }

#if !UNITY_WEBGL || UNITY_EDITOR
        ws.DispatchMessageQueue();
#endif
    }

    private void OnDestroy() {
        dataChannel.Close();
        connection.Close();
        ws.Close();
    }

    public void InitClient(string serverIp) {
        clientId = gameObject.name + "-REC";

        ws = new WebSocket($"wss://{serverIp}/", new Dictionary<string, string>() {
            { "user-agent", "unity webrtc datachannel" }
        });

        ws.OnOpen += () => {
            // STUN server config
            RTCConfiguration config = default;
            config.iceServers = new[] { new RTCIceServer { urls = new[] { "stun:stun.l.google.com:19302" } } };

            connection = new RTCPeerConnection(ref config);
            connection.OnIceCandidate = candidate => {
                //Debug.Log("ICE candidate generated: " + candidate.Candidate);

                var candidateInit = new CandidateInit() {
                    SdpMid = candidate.SdpMid,
                    SdpMLineIndex = candidate.SdpMLineIndex ?? 0,
                    Candidate = candidate.Candidate
                };
                ws.SendText("CANDIDATE!" + candidateInit.ConvertToJSON());
            };
            connection.OnIceConnectionChange = state => {
                Debug.Log(state);
            };

            connection.OnDataChannel = channel => {
                dataChannel = channel;
                dataChannel.OnMessage = bytes => {
                    var message = Encoding.UTF8.GetString(bytes);
                    Debug.Log("Receiver received: " + message);
                };
            };
        };

        ws.OnMessage += (bytes) => {
            var data = Encoding.UTF8.GetString(bytes);
            var requestArray = data.Split("!");
            var requestType = requestArray[0];
            var requestData = requestArray[1];

            switch (requestType) {
                case "OFFER":
                    Debug.Log(clientId + " - Got OFFER from Maximus: " + requestData);
                    receivedOfferSessionDescTemp = SessionDescription.FromJSON(requestData);
                    hasReceivedOffer = true;
                    break;
                case "CANDIDATE":
                    Debug.Log(clientId + " - Got CANDIDATE from Maximus: " + requestData);

                    // generate candidate data
                    var candidateInit = CandidateInit.FromJSON(requestData);
                    RTCIceCandidateInit init = new RTCIceCandidateInit();
                    init.sdpMid = candidateInit.SdpMid;
                    init.sdpMLineIndex = candidateInit.SdpMLineIndex;
                    init.candidate = candidateInit.Candidate;
                    RTCIceCandidate candidate = new RTCIceCandidate(init);

                    // add candidate to this connection
                    connection.AddIceCandidate(candidate);
                    break;
                default:
                    Debug.Log(clientId + " - Maximus says: " + data);
                    break;
            }
        };

        ws.Connect();
    }

    private IEnumerator CreateAnswer() {
        RTCSessionDescription offerSessionDesc = new RTCSessionDescription();
        offerSessionDesc.type = RTCSdpType.Offer;
        offerSessionDesc.sdp = receivedOfferSessionDescTemp.Sdp;

        var remoteDescOp = connection.SetRemoteDescription(ref offerSessionDesc);
        yield return remoteDescOp;

        var answer = connection.CreateAnswer();
        yield return answer;

        var answerDesc = answer.Desc;
        var localDescOp = connection.SetLocalDescription(ref answerDesc);
        yield return localDescOp;

        // send desc to server for sender connection
        var answerSessionDesc = new SessionDescription() {
            SessionType = answerDesc.type.ToString(),
            Sdp = answerDesc.sdp
        };
        ws.SendText("ANSWER!" + answerSessionDesc.ConvertToJSON());
    }
}