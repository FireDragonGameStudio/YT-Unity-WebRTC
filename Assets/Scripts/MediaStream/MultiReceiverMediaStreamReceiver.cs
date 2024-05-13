using System.Collections;
using Unity.WebRTC;
using UnityEngine;
using UnityEngine.UI;
using WebSocketSharp;

public class MultiReceiverMediaStreamReceiver : MonoBehaviour {
    [SerializeField] private RawImage receiveImage;

    private RTCPeerConnection connection;

    private WebSocket ws;
    private string clientId;

    private bool hasReceivedOffer = false;
    private SessionDescription receivedOfferSessionDescTemp;

    private string senderIp;
    private int senderPort;
    private int channelId;

    private void Start() {
        InitClient("192.168.0.207", 8080);
    }

    public void InitClient(string serverIp, int serverPort) {
        senderPort = serverPort == 0 ? 8080 : serverPort;
        senderIp = serverIp;

        clientId = gameObject.name;

        ws = new WebSocket($"ws://{senderIp}:{senderPort}/{nameof(MultiReceiverMediaChannelService)}");
        ws.OnMessage += (sender, e) => {
            var signalingMessage = new SignalingMessageChannel(e.Data);

            switch (signalingMessage.Type) {
                case SignalingMessageType.CHANNEL:
                    Debug.Log("RECEIVER received channel id: " + signalingMessage.ChannelId);

                    channelId = signalingMessage.ChannelId;

                    connection = new RTCPeerConnection();
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

                    connection.OnTrack = e => {
                        if (e.Track is VideoStreamTrack video) {
                            video.OnVideoReceived += tex => {
                                receiveImage.texture = tex;
                            };
                        }
                    };
                    break;
                case SignalingMessageType.OFFER:
                    Debug.Log(clientId + " - Got OFFER with channel ID " + signalingMessage.ChannelId + " from Maximus: " + signalingMessage.Message);

                    if (channelId == signalingMessage.ChannelId) {
                        receivedOfferSessionDescTemp = SessionDescription.FromJSON(signalingMessage.Message);
                        hasReceivedOffer = true;
                    }
                    break;
                case SignalingMessageType.CANDIDATE:
                    Debug.Log(clientId + " - Got CANDIDATE with channel ID " + signalingMessage.ChannelId + " from Maximus: " + signalingMessage.Message);

                    if (channelId == signalingMessage.ChannelId) {
                        // generate candidate data
                        var candidateInit = CandidateInit.FromJSON(signalingMessage.Message);
                        RTCIceCandidateInit init = new RTCIceCandidateInit();
                        init.sdpMid = candidateInit.SdpMid;
                        init.sdpMLineIndex = candidateInit.SdpMLineIndex;
                        init.candidate = candidateInit.Candidate;
                        RTCIceCandidate candidate = new RTCIceCandidate(init);

                        // add candidate to this connection
                        connection.AddIceCandidate(candidate);
                    }
                    break;
                default:
                    Debug.Log(clientId + " - Maximus says: " + e.Data);
                    break;
            }
        };
        ws.Connect();
        ws.Send("RECEIVER");

        StartCoroutine(WebRTC.Update());
    }

    private void Update() {
        if (hasReceivedOffer) {
            hasReceivedOffer = !hasReceivedOffer;
            StartCoroutine(CreateAnswer());
        }
    }

    private void OnDestroy() {
        connection.Close();
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
        ws.Send("ANSWER!" + channelId + "!" + answerSessionDesc.ConvertToJSON());
    }
}
