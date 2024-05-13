using System.Collections;
using System.Collections.Generic;
using Unity.WebRTC;
using UnityEngine;
using UnityEngine.UI;
using WebSocketSharp;

public class VideoChatMediaStream : MonoBehaviour {
    [SerializeField] private string clientId;
    [SerializeField] private Camera cameraStream;
    [SerializeField] private RawImage sourceImage;
    [SerializeField] private List<RawImage> receiveImages = new List<RawImage>();

    private Dictionary<string, RTCPeerConnection> pcs = new Dictionary<string, RTCPeerConnection>();

    private VideoStreamTrack videoStreamTrack;

    private bool hasReceivedOffer = false;
    private SessionDescription receivedOfferSessionDescTemp;
    private string receivedOfferChannelId;

    private WebSocket ws;

    private int receiveImageCounter = 0;

    private void Start() {
        InitClient("192.168.0.207", 8080);
    }

    private void Update() {
        if (hasReceivedOffer) {
            hasReceivedOffer = !hasReceivedOffer;
            StartCoroutine(CreateAnswer(pcs[receivedOfferChannelId], receivedOfferChannelId));
        }
    }

    private void OnDestroy() {
        videoStreamTrack.Stop();

        foreach (var connection in pcs) {
            connection.Value.Close();
        }

        ws.Close();
    }

    public void InitClient(string serverIp, int serverPort) {
        int port = serverPort == 0 ? 8080 : serverPort;

        ws = new WebSocket($"ws://{serverIp}:{port}/{nameof(VideoChatMediaStreamService)}");
        ws.OnMessage += (sender, e) => {
            var signalingMessage = new SignalingMessageVideoChat(e.Data);

            switch (signalingMessage.Type) {
                case SignalingMessageType.OFFER:
                    // only set offer and send answer on receiving connections on this client
                    if (clientId == signalingMessage.ChannelId.Substring(1, 1)) {
                        Debug.Log(clientId + " - Got OFFER with channel ID " + signalingMessage.ChannelId + " from Maximus: " + signalingMessage.Message);

                        receivedOfferChannelId = signalingMessage.ChannelId;
                        receivedOfferSessionDescTemp = SessionDescription.FromJSON(signalingMessage.Message);
                        hasReceivedOffer = true;
                    }

                    break;
                case SignalingMessageType.ANSWER:
                    // only set answer for sending connections on this client
                    if (clientId == signalingMessage.ChannelId.Substring(0, 1)) {
                        Debug.Log(clientId + " - Got ANSWER with channel ID " + signalingMessage.ChannelId + " from Maximus: " + signalingMessage.Message);

                        var receivedAnswerSessionDescTemp = SessionDescription.FromJSON(signalingMessage.Message);
                        RTCSessionDescription answerSessionDesc = new RTCSessionDescription();
                        answerSessionDesc.type = RTCSdpType.Answer;
                        answerSessionDesc.sdp = receivedAnswerSessionDescTemp.Sdp;

                        pcs[signalingMessage.ChannelId].SetRemoteDescription(ref answerSessionDesc);
                    }

                    break;
                case SignalingMessageType.CANDIDATE:
                    // only set candidates on receiving connections for this client
                    if (clientId == signalingMessage.ChannelId.Substring(1, 1)) {
                        Debug.Log(clientId + " - Got CANDIDATE with channel ID " + signalingMessage.ChannelId + " from Maximus: " + signalingMessage.Message);

                        // generate candidate data
                        var candidateInit = CandidateInit.FromJSON(signalingMessage.Message);
                        RTCIceCandidateInit init = new RTCIceCandidateInit();
                        init.sdpMid = candidateInit.SdpMid;
                        init.sdpMLineIndex = candidateInit.SdpMLineIndex;
                        init.candidate = candidateInit.Candidate;
                        RTCIceCandidate candidate = new RTCIceCandidate(init);

                        // add candidate to this connection
                        pcs[signalingMessage.ChannelId].AddIceCandidate(candidate);
                    }

                    break;
                default:
                    if (e.Data.Contains("|")) {
                        var connectionIds = e.Data.Split("|");
                        foreach (var connectionId in connectionIds) {
                            // only add relevant sending/receiving connections for this client
                            // e.g. for client 1 -> sending: 10, 12 receiving: 01, 21
                            if (connectionId.Contains(clientId)) {
                                pcs.Add(connectionId, CreatePeerConnection(connectionId));
                            }
                        }
                    } else {
                        // set client id, based on connection order
                        clientId = e.Data;
                    }

                    break;
            }
        };
        ws.Connect();

        videoStreamTrack = cameraStream.CaptureStreamTrack(1280, 720);
        sourceImage.texture = cameraStream.targetTexture;

        StartCoroutine(WebRTC.Update());
    }

    private RTCPeerConnection CreatePeerConnection(string id) {
        var pc = new RTCPeerConnection();
        pc.OnIceCandidate = candidate => {
            var candidateInit = new CandidateInit() {
                SdpMid = candidate.SdpMid,
                SdpMLineIndex = candidate.SdpMLineIndex ?? 0,
                Candidate = candidate.Candidate
            };
            ws.Send("CANDIDATE!" + id + "!" + candidateInit.ConvertToJSON());
        };
        pc.OnIceConnectionChange = state => {
            Debug.Log(state);
        };

        pc.OnNegotiationNeeded = () => {
            StartCoroutine(CreateOffer(pc, id));
        };

        pc.OnTrack = e => {
            if (e.Track is VideoStreamTrack track) {
                track.OnVideoReceived += tex => {
                    receiveImages[receiveImageCounter].texture = tex;
                    receiveImageCounter++;
                };
            }
        };

        return pc;
    }

    private IEnumerator CreateOffer(RTCPeerConnection pc, string id) {
        var offer = pc.CreateOffer();
        yield return offer;

        var offerDesc = offer.Desc;
        var localDescOp = pc.SetLocalDescription(ref offerDesc);
        yield return localDescOp;

        // send desc to server for receiver connection
        var offerSessionDesc = new SessionDescription() {
            SessionType = offerDesc.type.ToString(),
            Sdp = offerDesc.sdp
        };
        ws.Send("OFFER!" + id + "!" + offerSessionDesc.ConvertToJSON());
    }

    private IEnumerator CreateAnswer(RTCPeerConnection pc, string id) {
        RTCSessionDescription offerSessionDesc = new RTCSessionDescription();
        offerSessionDesc.type = RTCSdpType.Offer;
        offerSessionDesc.sdp = receivedOfferSessionDescTemp.Sdp;

        var remoteDescOp = pc.SetRemoteDescription(ref offerSessionDesc);
        yield return remoteDescOp;

        var answer = pc.CreateAnswer();
        yield return answer;

        var answerDesc = answer.Desc;
        var localDescOp = pc.SetLocalDescription(ref answerDesc);
        yield return localDescOp;

        // send desc to server for sender connection
        var answerSessionDesc = new SessionDescription() {
            SessionType = answerDesc.type.ToString(),
            Sdp = answerDesc.sdp
        };
        ws.Send("ANSWER!" + id + "!" + answerSessionDesc.ConvertToJSON());
    }

    public void Call() {
        foreach (var connection in pcs) {
            // only add tracks for sending connections
            // aka first number is client id
            if (connection.Key.Substring(0, 1) == clientId) {
                connection.Value.AddTrack(videoStreamTrack);
            }
        }
    }
}
