// Assets/Editor/MessageEntry.cs (또는 해당 파일)
using System;

[System.Serializable]
public class MessageEntry
{
    public string Content;
    public MessageType Type;
    public DateTime Timestamp; // ⭐ 추가: 메시지 생성 시간

    public enum MessageType { User, AI }

    public MessageEntry(string content, MessageType type)
    {
        Content = content;
        Type = type;
        Timestamp = DateTime.Now; // ⭐ 현재 시간으로 초기화
    }
}