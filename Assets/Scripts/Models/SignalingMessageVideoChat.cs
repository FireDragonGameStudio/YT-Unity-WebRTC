using System;

public class SignalingMessageVideoChat {

    public readonly SignalingMessageType Type;
    public readonly string ChannelId;
    public readonly string Message;

    public SignalingMessageVideoChat(string messageString) {
        // default values
        Type = SignalingMessageType.OTHER;
        ChannelId = "";
        Message = messageString;

        var messageArray = messageString.Split("!");

        if ((messageArray.Length >= 3) && Enum.TryParse(messageArray[0], out SignalingMessageType resultType)) {
            switch (resultType) {
                case SignalingMessageType.OFFER:
                case SignalingMessageType.ANSWER:
                case SignalingMessageType.CANDIDATE:
                case SignalingMessageType.CHANNEL:
                case SignalingMessageType.SENDER:
                case SignalingMessageType.RECEIVER:
                    Type = resultType;
                    ChannelId = messageArray[1];
                    Message = messageArray[2];
                    break;
            }
        }
    }
}
