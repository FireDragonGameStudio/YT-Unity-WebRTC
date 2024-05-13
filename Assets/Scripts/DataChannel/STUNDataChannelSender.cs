using NativeWebSocket;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Unity.WebRTC;
using UnityEngine;

public class STUNDataChannelSender : MonoBehaviour {
    [SerializeField] private bool sendMessageViaChannel = false;
    [SerializeField] private bool sendTestMessage = false;

    private RTCPeerConnection connection;
    private RTCDataChannel dataChannel;

    private WebSocket ws;
    private string clientId;

    private bool hasReceivedAnswer = false;
    private SessionDescription receivedAnswerSessionDescTemp;

    private async void Start() {

        clientId = gameObject.name;

        ws = new WebSocket("wss://unity-stun-signaling.glitch.me/", new Dictionary<string, string>() {
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

            dataChannel = connection.CreateDataChannel("sendChannel");
            dataChannel.OnOpen = () => {
                Debug.Log("Sender opened channel");
            };
            dataChannel.OnClose = () => {
                Debug.Log("Sender closed channel");
            };

            connection.OnNegotiationNeeded = () => {
                StartCoroutine(CreateOffer());
            };
        };

        ws.OnMessage += (bytes) => {
            var data = Encoding.UTF8.GetString(bytes);
            var requestArray = data.Split("!");
            var requestType = requestArray[0];
            var requestData = requestArray[1];

            switch (requestType) {
                case "ANSWER":
                    Debug.Log(clientId + " - Got ANSWER from Maximus: " + requestData);
                    receivedAnswerSessionDescTemp = SessionDescription.FromJSON(requestData);
                    hasReceivedAnswer = true;
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

        await ws.Connect();
    }

    private void Update() {
        if (hasReceivedAnswer) {
            hasReceivedAnswer = !hasReceivedAnswer;
            StartCoroutine(SetRemoteDesc());
        }
        if (sendMessageViaChannel) {
            sendMessageViaChannel = !sendMessageViaChannel;
            dataChannel.Send("TEST!WEBRTC DATACHANNEL TEST");
        }
        if (sendTestMessage) {
            sendTestMessage = !sendTestMessage;
            ws.SendText("TEST!WEBSOCKET TEST");
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

    private IEnumerator CreateOffer() {
        var offer = connection.CreateOffer();
        yield return offer;

        var offerDesc = offer.Desc;
        var localDescOp = connection.SetLocalDescription(ref offerDesc);
        yield return localDescOp;

        // send desc to server for receiver connection
        var offerSessionDesc = new SessionDescription() {
            SessionType = offerDesc.type.ToString(),
            Sdp = offerDesc.sdp
        };
        ws.SendText("OFFER!" + offerSessionDesc.ConvertToJSON());
    }

    private IEnumerator SetRemoteDesc() {
        RTCSessionDescription answerSessionDesc = new RTCSessionDescription();
        answerSessionDesc.type = RTCSdpType.Answer;
        answerSessionDesc.sdp = receivedAnswerSessionDescTemp.Sdp;

        var remoteDescOp = connection.SetRemoteDescription(ref answerSessionDesc);
        yield return remoteDescOp;
    }
}