// Editor/MessageEntry.cs
public class MessageEntry
{
    public string Content;
    public MessageType Type;
    public System.DateTime Timestamp;

    public enum MessageType
    {
        User,
        AI
    }

    public MessageEntry(string content, MessageType type)
    {
        Content = content;
        Type = type;
        Timestamp = System.DateTime.Now;
    }
}