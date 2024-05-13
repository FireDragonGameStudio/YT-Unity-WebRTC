using System.Collections;
using System.Collections.Generic;
using Unity.WebRTC;
using UnityEngine;
using UnityEngine.UI;
using WebSocketSharp;

public class MultiReceiverMediaStreamSender : MonoBehaviour {
    [SerializeField] private Camera cameraStream;
    [SerializeField] private RawImage sourceImage;

    private VideoStreamTrack videoStreamTrack;

    private WebSocket ws;
    private string clientId;
    private Dictionary<int, RTCPeerConnection> channels = new Dictionary<int, RTCPeerConnection>();

    private void Start() {
        InitClient("192.168.0.207", 8080);
    }

    public void InitClient(string serverIp, int serverPort) {
        int port = serverPort == 0 ? 8080 : serverPort;
        clientId = gameObject.name;

        ws = new WebSocket($"ws://{serverIp}:{port}/{nameof(MultiReceiverMediaChannelService)}");
        ws.OnMessage += (sender, e) => {
            var signalingMessage = new SignalingMessageChannel(e.Data);

            switch (signalingMessage.Type) {
                case SignalingMessageType.CHANNEL:
                    Debug.Log("SENDER received channel id: " + signalingMessage.ChannelId);

                    var channelId = signalingMessage.ChannelId;
                    var connection = new RTCPeerConnection();
                    connection.OnIceCandidate = candidate => {
                        var candidateInit = new CandidateInit() {
                            SdpMid = candidate.SdpMid,
                            SdpMLineIndex = candidate.SdpMLineIndex ?? 0,
                            Candidate = candidate.Candidate
                        };
                        ws.Send("CANDIDATE!" + channelId + "!" + candidateInit.ConvertToJSON());
                    };
                    connection.OnIceConnectionChange = state => {
                        Debug.Log(state);
                    };

                    connection.OnNegotiationNeeded = () => {
                        StartCoroutine(CreateOffer(connection, channelId));
                    };

                    connection.AddTrack(videoStreamTrack);

                    channels.Add(channelId, connection);
                    break;
                case SignalingMessageType.ANSWER:
                    Debug.Log(clientId + " - Got ANSWER with channel ID " + signalingMessage.ChannelId + " from Maximus: " + signalingMessage.Message);

                    var answerChannelId = signalingMessage.ChannelId;

                    var receivedAnswerSessionDescTemp = SessionDescription.FromJSON(signalingMessage.Message);
                    RTCSessionDescription answerSessionDesc = new RTCSessionDescription();
                    answerSessionDesc.type = RTCSdpType.Answer;
                    answerSessionDesc.sdp = receivedAnswerSessionDescTemp.Sdp;

                    channels[answerChannelId].SetRemoteDescription(ref answerSessionDesc);
                    break;
                case SignalingMessageType.CANDIDATE:
                    Debug.Log(clientId + " - Got CANDIDATE with channel ID " + signalingMessage.ChannelId + " from Maximus: " + signalingMessage.Message);

                    var candidateChannelId = signalingMessage.ChannelId;

                    // generate candidate data
                    var candidateInit = CandidateInit.FromJSON(signalingMessage.Message);
                    RTCIceCandidateInit init = new RTCIceCandidateInit();
                    init.sdpMid = candidateInit.SdpMid;
                    init.sdpMLineIndex = candidateInit.SdpMLineIndex;
                    init.candidate = candidateInit.Candidate;
                    RTCIceCandidate candidate = new RTCIceCandidate(init);

                    // add candidate to this connection
                    channels[candidateChannelId].AddIceCandidate(candidate);
                    break;
                default:
                    Debug.Log(clientId + " - Maximus says: " + e.Data);
                    break;
            }
        };
        ws.Connect();
        ws.Send("SENDER");

        videoStreamTrack = cameraStream.CaptureStreamTrack(1280, 720);
        sourceImage.texture = cameraStream.targetTexture;

        StartCoroutine(WebRTC.Update());
    }

    private void OnDestroy() {
        foreach (var channel in channels) {
            channel.Value.Close();
        }
    }

    private IEnumerator CreateOffer(RTCPeerConnection pc, int channelId) {
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
        ws.Send("OFFER!" + channelId + "!" + offerSessionDesc.ConvertToJSON());
    }
}
