using System.Collections;
using Unity.WebRTC;
using UnityEngine;
using WebSocketSharp;

public class SimpleDataChannelSender : MonoBehaviour {
    [SerializeField] private bool sendMessageViaChannel = false;

    private RTCPeerConnection connection;
    private RTCDataChannel dataChannel;

    private WebSocket ws;
    private string clientId;

    private bool hasReceivedAnswer = false;
    private SessionDescription receivedAnswerSessionDescTemp;

    private void Start() {
        InitClient("192.168.0.207", 8080);
    }

    private void Update() {
        if (hasReceivedAnswer) {
            hasReceivedAnswer = !hasReceivedAnswer;
            StartCoroutine(SetRemoteDesc());
        }
        if (sendMessageViaChannel) {
            sendMessageViaChannel = !sendMessageViaChannel;
            dataChannel.Send("TEST!TEST TEST");
        }
    }

    private void OnDestroy() {
        dataChannel.Close();
        connection.Close();
    }

    public void InitClient(string serverIp, int serverPort) {
        int port = serverPort == 0 ? 8080 : serverPort;
        clientId = gameObject.name;

        ws = new WebSocket($"ws://{serverIp}:{port}/{nameof(SimpleDataChannelService)}");
        ws.OnMessage += (sender, e) => {
            var requestArray = e.Data.Split("!");
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
                    Debug.Log(clientId + " - Maximus says: " + e.Data);
                    break;
            }
        };
        ws.Connect();

        connection = new RTCPeerConnection();
        connection.OnIceCandidate = candidate => {
            //Debug.Log("ICE candidate generated: " + candidate.Candidate);

            var candidateInit = new CandidateInit() {
                SdpMid = candidate.SdpMid,
                SdpMLineIndex = candidate.SdpMLineIndex ?? 0,
                Candidate = candidate.Candidate
            };
            ws.Send("CANDIDATE!" + candidateInit.ConvertToJSON());
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
        ws.Send("OFFER!" + offerSessionDesc.ConvertToJSON());
    }

    private IEnumerator SetRemoteDesc() {
        RTCSessionDescription answerSessionDesc = new RTCSessionDescription();
        answerSessionDesc.type = RTCSdpType.Answer;
        answerSessionDesc.sdp = receivedAnswerSessionDescTemp.Sdp;

        var remoteDescOp = connection.SetRemoteDescription(ref answerSessionDesc);
        yield return remoteDescOp;
    }
}