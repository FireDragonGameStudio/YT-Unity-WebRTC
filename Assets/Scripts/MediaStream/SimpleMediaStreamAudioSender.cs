using System.Collections;
using Unity.WebRTC;
using UnityEngine;
using WebSocketSharp;

public class SimpleMediaStreamAudioSender : MonoBehaviour {

    [SerializeField] private AudioSource inputAudioSource;

    private RTCPeerConnection connection;
    private MediaStream sendStream;
    private AudioStreamTrack audioStreamTrack;

    private WebSocket ws;
    private string clientId;

    private bool hasReceivedAnswer = false;
    private SessionDescription receivedAnswerSessionDescTemp;

    private void Start() {
        InitClient("192.168.0.51", 8080);
    }

    public void InitClient(string serverIp, int serverPort) {
        int port = serverPort == 0 ? 8080 : serverPort;
        clientId = gameObject.name;

        ws = new WebSocket($"ws://{serverIp}:{port}/{nameof(SimpleDataChannelService)}");
        ws.OnMessage += (sender, e) => {
            var signalingMessage = new SignalingMessage(e.Data);

            switch (signalingMessage.Type) {
                case SignalingMessageType.ANSWER:
                    Debug.Log($"{clientId} - Got ANSWER from Maximus: {signalingMessage.Message}");
                    receivedAnswerSessionDescTemp = SessionDescription.FromJSON(signalingMessage.Message);
                    hasReceivedAnswer = true;
                    break;
                case SignalingMessageType.CANDIDATE:
                    Debug.Log($"{clientId} - Got CANDIDATE from Maximus: {signalingMessage.Message}");

                    // generate candidate data
                    var candidateInit = CandidateInit.FromJSON(signalingMessage.Message);
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

        connection.OnNegotiationNeeded = () => {
            StartCoroutine(CreateOffer());
        };

        // play audio on sender side for transmission
        inputAudioSource.loop = true;
        inputAudioSource.Play();

        audioStreamTrack = new AudioStreamTrack(inputAudioSource);
        audioStreamTrack.Loopback = true;
        connection.AddTrack(audioStreamTrack, sendStream);

        StartCoroutine(WebRTC.Update());
    }

    private void Update() {
        if (hasReceivedAnswer) {
            hasReceivedAnswer = !hasReceivedAnswer;
            StartCoroutine(SetRemoteDesc());
        }
    }

    private void OnDestroy() {
        audioStreamTrack.Stop();
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
