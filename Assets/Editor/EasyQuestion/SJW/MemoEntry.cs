using System;

[System.Serializable]
public class MemoEntry
{
    public enum MessageType
    {
        User,
        AI,
        Info,
        Warning,
        Error,
        Memo
    }
    
    public string Content;
    public MessageType Type; 
    public DateTime Timestamp;

    public MemoEntry(string content, MessageType type, DateTime timestamp)
    {
        Content = content;
        Type = type;
        Timestamp = timestamp;
    }

    public MemoEntry(string content, DateTime timestamp) : this(content, MessageType.Memo, timestamp) { }

    public MemoEntry(string content, MessageType type) : this(content, type, DateTime.Now) { }
}