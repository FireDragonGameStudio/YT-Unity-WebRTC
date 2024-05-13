using System;

public class SignalingMessageChannel {

    public readonly SignalingMessageType Type;
    public readonly int ChannelId;
    public readonly string Message;

    public SignalingMessageChannel(string messageString) {
        // default values
        Type = SignalingMessageType.OTHER;
        ChannelId = 0;
        Message = messageString;

        var messageArray = messageString.Split("!");

        if ((messageArray.Length >= 3) && Enum.TryParse(messageArray[0], out SignalingMessageType resultType)) {
            switch (resultType) {
                case SignalingMessageType.OFFER:
                case SignalingMessageType.ANSWER:
                case SignalingMessageType.CANDIDATE:
                case SignalingMessageType.CHANNEL:
                    Type = resultType;
                    ChannelId = int.Parse(messageArray[1]);
                    Message = messageArray[2];
                    break;
            }
        }
    }
}
