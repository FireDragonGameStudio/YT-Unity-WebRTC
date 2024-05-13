using System;

public class SignalingMessage {

    public readonly SignalingMessageType Type;
    public readonly string Message;

    public SignalingMessage(string messageString) {
        Type = SignalingMessageType.OTHER;
        Message = messageString;

        var messageArray = messageString.Split("!");

        if ((messageArray.Length >= 2) && Enum.TryParse(messageArray[0], out SignalingMessageType resultType)) {
            switch (resultType) {
                case SignalingMessageType.OFFER:
                case SignalingMessageType.ANSWER:
                case SignalingMessageType.CANDIDATE:
                    Type = resultType;
                    Message = messageArray[1];
                    break;
            }
        }
    }
}
