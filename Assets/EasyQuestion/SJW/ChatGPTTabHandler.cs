using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Diagnostics;
using Debug = UnityEngine.Debug;
using UnityEngine.Networking;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Linq;
using System;

[System.Serializable]
public class ChatGPTTabHandler
{
    private string chatGPTQuery = "";
    private Vector2 chatGPTScrollPos;
    private List<MessageEntry> chatGPTMessages = new List<MessageEntry>();
    private string chatGPTApiKey = "";
    private string[] availableChatGPTModels = { "gpt-3.5-turbo", "gpt-4", "gpt-4o" };
    private string chatGPTAiVersion = "gpt-3.5-turbo";
    private bool isApiKeyApproved = false;
    private bool isSendingRequest = false;
    private bool isApprovingApiKey = false;
    private bool showServiceSwapWarning = false;

    private string apiKeyFilePath;
    private string scriptFolderPath;

    private GeminiChatGPTIntegrationEditor _parentWindow;

    public ChatGPTTabHandler()
    {
    }

    public void Initialize(EditorWindow parentWindow)
    {
        _parentWindow = parentWindow as GeminiChatGPTIntegrationEditor;

        if (string.IsNullOrEmpty(scriptFolderPath))
        {
            string scriptPath = AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(parentWindow));
            scriptFolderPath = Path.GetDirectoryName(scriptPath);
            apiKeyFilePath = Path.Combine(scriptFolderPath, "chatgpt_api_key.txt");
            LoadApiKey();
        }

        if (chatGPTMessages.Count == 0)
        {
            chatGPTMessages.Add(new MessageEntry("나: 안녕하세요, 유니티 에디터에서 ChatGPT 연동 테스트 중입니다.", MessageEntry.MessageType.User));
            chatGPTMessages.Add(new MessageEntry("ChatGPT: 반갑습니다! 어떤 것을 도와드릴까요?", MessageEntry.MessageType.AI));
        }
    }

    private void LoadApiKey()
    {
        if (File.Exists(apiKeyFilePath))
        {
            chatGPTApiKey = File.ReadAllText(apiKeyFilePath).Trim();
            isApiKeyApproved = !string.IsNullOrEmpty(chatGPTApiKey);
        }
    }

    private async void SaveApiKeyAndValidate()
    {
        if (string.IsNullOrEmpty(chatGPTApiKey))
        {
            EditorUtility.DisplayDialog("경고", "API 키를 입력해주세요.", "확인");
            return;
        }

        isApprovingApiKey = true;
        _parentWindow.Repaint();

        string testUrl = "https://api.openai.com/v1/models";
        using (UnityWebRequest request = UnityWebRequest.Get(testUrl))
        {
            request.SetRequestHeader("Authorization", "Bearer " + chatGPTApiKey.Trim());
            var operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                await Task.Yield();
            }

            if (request.result == UnityWebRequest.Result.Success)
            {
                File.WriteAllText(apiKeyFilePath, chatGPTApiKey.Trim());
                AssetDatabase.Refresh();
                Debug.Log("ChatGPT API Key 저장 및 유효성 검사 완료: " + apiKeyFilePath);
                isApiKeyApproved = true;
                EditorUtility.DisplayDialog("성공", "API 키가 성공적으로 승인되었습니다.", "확인");
            }
            else
            {
                isApiKeyApproved = false;
                string errorMessage = request.responseCode == 401 ?
                                      "ChatGPT: 오류 - 올바르지 않은 API 키입니다. 키를 확인하고 다시 시도해주세요." :
                                      $"ChatGPT: API 키 유효성 검사 실패 - {request.error} (코드: {request.responseCode})";
                Debug.LogError(errorMessage + "\n" + request.downloadHandler.text);
                EditorUtility.DisplayDialog("오류", errorMessage, "확인");
            }
        }
        
        isApprovingApiKey = false;
        _parentWindow.Repaint();
    }


    public void OnGUI(float editorWindowWidth, float editorWindowHeight)
    {
        EditorGUILayout.LabelField("✨ ChatGPT와 대화하기", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("🔑 API 설정", EditorStyles.boldLabel);

        if (!isApiKeyApproved)
        {
            EditorGUILayout.HelpBox("ChatGPT API 키가 입력되지 않았거나 유효하지 않습니다. 아래 버튼을 눌러 발급받거나 입력 후 승인해주세요.", MessageType.Warning);
            if (GUILayout.Button("🚀 ChatGPT API 키 발급 페이지로 이동", GUILayout.Height(30)))
            {
                Application.OpenURL("https://platform.openai.com/account/api-keys");
            }
        }

        EditorGUILayout.BeginHorizontal();
        GUI.enabled = !isApiKeyApproved && !isApprovingApiKey;
        chatGPTApiKey = EditorGUILayout.TextField("API Key:", chatGPTApiKey);
        GUI.enabled = true;

        if (!isApiKeyApproved)
        {
            GUI.enabled = !isApprovingApiKey;
            if (GUILayout.Button(isApprovingApiKey ? "확인 중..." : "✅ 승인", GUILayout.Width(80), GUILayout.Height(25)))
            {
                SaveApiKeyAndValidate();
            }
            GUI.enabled = true;
        }
        else
        {
            if (GUILayout.Button("✏️ 수정", GUILayout.Width(80), GUILayout.Height(25)))
            {
                isApiKeyApproved = false;
            }
        }
        EditorGUILayout.EndHorizontal();

        int currentModelIndex = System.Array.IndexOf(availableChatGPTModels, chatGPTAiVersion);
        int newModelIndex = EditorGUILayout.Popup("🤖 현재 구동 중인 AI 모델:", currentModelIndex, availableChatGPTModels);
        if (newModelIndex != currentModelIndex)
        {
            chatGPTAiVersion = availableChatGPTModels[newModelIndex];
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(10);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.ExpandHeight(true));
        EditorGUILayout.LabelField("💬 대화 내용", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);

        // 스크롤 뷰 시작
        chatGPTScrollPos = EditorGUILayout.BeginScrollView(chatGPTScrollPos, GUILayout.ExpandHeight(true));
        
        GUIStyle chatStyle = new GUIStyle(EditorStyles.wordWrappedLabel);
        chatStyle.normal.textColor = EditorStyles.label.normal.textColor;
        chatStyle.padding = new RectOffset(5, 5, 5, 5);
        chatStyle.richText = true;

        StringBuilder fullChatContent = new StringBuilder();

        List<MessageEntry> messagesToDisplay = new List<MessageEntry>();
        if (chatGPTMessages.Any())
        {
            DateTime lastMessageTime = chatGPTMessages.Last().Timestamp;
            DateTime oneHourAgo = lastMessageTime.AddHours(-1);

            messagesToDisplay = chatGPTMessages
                .Where(entry => entry.Timestamp >= oneHourAgo)
                .ToList();
        }

        foreach (MessageEntry entry in messagesToDisplay)
        {
            if (entry.Type == MessageEntry.MessageType.User)
            {
                fullChatContent.AppendLine($"<color=white><b>나 ({entry.Timestamp:HH:mm:ss}):</b> {entry.Content}</color>\n");
            }
            else
            {
                fullChatContent.AppendLine($"<color=#ADD8E6><b>ChatGPT ({entry.Timestamp:HH:mm:ss}):</b> {entry.Content}</color>\n");
            }
        }

        // 스크롤 가능한 SelectableLabel
        EditorGUILayout.SelectableLabel(fullChatContent.ToString(), chatStyle, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

        EditorGUILayout.EndScrollView(); // 스크롤 뷰 끝
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(10);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Height(200)); // 질문 섹션 고정 높이
        EditorGUILayout.LabelField("✏️ 질문하기", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);

        if (chatGPTMessages.Count > 1)
        {
            EditorGUILayout.LabelField($"가장 최근 질문: {chatGPTMessages[chatGPTMessages.Count - 2].Content}", EditorStyles.miniLabel);
        }

        GUI.enabled = isApiKeyApproved && !isSendingRequest;

        chatGPTQuery = EditorGUILayout.TextArea(chatGPTQuery, GUILayout.MinHeight(80)); // 최소 높이 설정

        EditorGUILayout.Space(5);

        if (GUILayout.Button(isSendingRequest ? "⏳ 전송 중..." : "⬆️ 전송", GUILayout.Height(35)))
        {
            if (!isApiKeyApproved)
            {
                EditorUtility.DisplayDialog("경고", "ChatGPT API 키가 승인되지 않았습니다. API 키를 입력하고 승인해주세요.", "확인");
            }
            else if (string.IsNullOrEmpty(chatGPTQuery.Trim()))
            {
                EditorUtility.DisplayDialog("경고", "질문 내용을 입력해주세요.", "확인");
            }
            else if (chatGPTAiVersion == "invalid-model")
            {
                 EditorUtility.DisplayDialog("경고", $"현재 선택된 모델 '{chatGPTAiVersion}'은(는) 유효하지 않습니다. 다른 모델을 선택해주세요.", "확인");
            }
            else
            {
                SendChatGPTQuery(chatGPTQuery);
                chatGPTQuery = "";
                showServiceSwapWarning = false;
            }
        }
        GUI.enabled = true;
        
        if (isSendingRequest)
        {
            EditorGUILayout.HelpBox("ChatGPT 응답을 기다리는 중...", MessageType.Info);
        }
        else if (isApprovingApiKey)
        {
            EditorGUILayout.HelpBox("API 키 유효성 확인 중...", MessageType.Info);
        }
        else if (showServiceSwapWarning)
        {
            EditorGUILayout.HelpBox("⚠️ 답변이 제대로 오지 않았습니다. 다른 구동 서비스(Gemini 탭)로 교체해보세요.", MessageType.Warning);
        }
        EditorGUILayout.EndVertical();
    }

    public async void SendChatGPTQuery(string query, bool isFromStatistics = false)
    {
        if (string.IsNullOrEmpty(query)) return;
        if (!isApiKeyApproved)
        {
            if (!isFromStatistics) EditorUtility.DisplayDialog("경고", "API 키를 먼저 승인해주세요.", "확인");
            return;
        }

        isSendingRequest = true;
        showServiceSwapWarning = false;
        _parentWindow.Repaint();

        chatGPTMessages.Add(new MessageEntry(query, MessageEntry.MessageType.User));
        chatGPTMessages.Add(new MessageEntry("답변 생성 중...", MessageEntry.MessageType.AI));
        
        chatGPTScrollPos.y = float.MaxValue;

        string responseText = "오류: 응답을 받지 못했습니다.";

        try
        {
            string url = "https://api.openai.com/v1/chat/completions";
            
            List<Dictionary<string, string>> rawMessages = new List<Dictionary<string, string>>();
            
            if (chatGPTMessages.Count > 1) 
            {
                int historyStartIdx = Mathf.Max(0, chatGPTMessages.Count - 6); 
                for (int i = historyStartIdx; i < chatGPTMessages.Count - 1; i++) 
                {
                    var msg = chatGPTMessages[i];
                    if (msg.Type == MessageEntry.MessageType.User)
                    {
                        rawMessages.Add(new Dictionary<string, string> { { "role", "user" }, { "content", msg.Content } });
                    }
                    else if (msg.Type == MessageEntry.MessageType.AI)
                    {
                        rawMessages.Add(new Dictionary<string, string> { { "role", "assistant" }, { "content", msg.Content } });
                    }
                }
            }
            rawMessages.Add(new Dictionary<string, string> { { "role", "user" }, { "content", query } });

            MessageEntryForChatGPT[] formattedMessages = rawMessages
                .Select(dict => new MessageEntryForChatGPT {
                    role = dict["role"],
                    content = dict["content"]
                })
                .ToArray();

            var requestBody = new OpenAIRequestPayload
            {
                model = chatGPTAiVersion,
                messages = formattedMessages
            };

            string jsonPayload = JsonUtility.ToJson(requestBody);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);

            using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Authorization", "Bearer " + chatGPTApiKey);

                var operation = request.SendWebRequest();
                while (!operation.isDone)
                {
                    await Task.Yield();
                }

                if (request.result == UnityWebRequest.Result.ProtocolError && request.responseCode == 401)
                {
                    responseText = "ChatGPT: 오류 - 올바르지 않은 API 키입니다. 키를 확인하고 다시 시도해주세요.";
                    Debug.LogError($"ChatGPT API Key Error: {request.downloadHandler.text}");
                    isApiKeyApproved = false;
                    showServiceSwapWarning = true;
                }
                else if (request.result == UnityWebRequest.Result.Success)
                {
                    string jsonResponse = request.downloadHandler.text;
                    ChatGPTResponse chatGPTResponse = JsonUtility.FromJson<ChatGPTResponse>(jsonResponse);

                    if (chatGPTResponse != null && chatGPTResponse.choices != null && chatGPTResponse.choices.Length > 0 &&
                        chatGPTResponse.choices[0].message != null && !string.IsNullOrEmpty(chatGPTResponse.choices[0].message.content))
                    {
                        responseText = chatGPTResponse.choices[0].message.content.Trim();
                        showServiceSwapWarning = false;
                    }
                    else if (chatGPTResponse != null && chatGPTResponse.error != null)
                    {
                        responseText = $"ChatGPT: 오류 - {chatGPTResponse.error.message}";
                        Debug.LogError(responseText);
                        showServiceSwapWarning = true;
                    }
                    else
                    {
                        responseText = "오류: ChatGPT 응답 파싱 실패. 원본: " + jsonResponse;
                        Debug.LogError(responseText);
                        showServiceSwapWarning = true;
                    }
                }
                else
                {
                    responseText = $"오류: {request.error} - {request.downloadHandler.text}";
                    Debug.LogError(responseText);
                    showServiceSwapWarning = true;
                }
            }
        }
        catch (System.Exception e)
        {
            responseText = "예외 발생: " + e.Message;
            Debug.LogError(responseText);
            showServiceSwapWarning = true;
        }
        finally
        {
            if (chatGPTMessages.Count > 0 && chatGPTMessages[chatGPTMessages.Count - 1].Content == "답변 생성 중...")
            {
                chatGPTMessages[chatGPTMessages.Count - 1].Content = responseText;
                chatGPTMessages[chatGPTMessages.Count - 1].Timestamp = DateTime.Now;
            }
            else
            {
                chatGPTMessages.Add(new MessageEntry(responseText, MessageEntry.MessageType.AI));
            }

            GeminiChatGPTIntegrationEditor editorWindow = _parentWindow;
            if (editorWindow != null)
            {
                QuestionListTabHandler questionListHandler = editorWindow.GetQuestionListTabHandler();
                if (questionListHandler != null)
                {
                    questionListHandler.AddQuestion(query, responseText, AiServiceType.ChatGPT);
                    if (!isFromStatistics)
                    {
                         StatisticsTabHandler statsHandler = editorWindow.GetStatisticsTabHandler();
                         if (statsHandler != null)
                         {
                             statsHandler.RecordKeyword(query);
                         }
                    }
                }
            }

            isSendingRequest = false;
            chatGPTScrollPos.y = float.MaxValue;
            _parentWindow.Repaint();
        }
    }

    private string EscapeJsonString(string rawString)
    {
        if (string.IsNullOrEmpty(rawString))
        {
            return "";
        }
        return rawString.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
    }

    [System.Serializable]
    private class OpenAIRequestPayload
    {
        public string model;
        public MessageEntryForChatGPT[] messages;
        public float temperature = 0.7f;
    }

    [System.Serializable]
    private class MessageEntryForChatGPT
    {
        public string role;
        public string content;
    }

    [System.Serializable]
    private class ChatGPTResponse
    {
        public Choice[] choices;
        public Error error;
    }

    [System.Serializable]
    private class Choice
    {
        public MessageEntryForChatGPT message;
    }

    [System.Serializable]
    private class Error
    {
        public string message;
        public string type;
        public string param;
        public string code;
    }

    [System.Serializable]
    public class MessageEntry
    {
        public string Content;
        public MessageType Type;
        public DateTime Timestamp;

        public enum MessageType { User, AI }

        public MessageEntry(string content, MessageType type)
        {
            Content = content;
            Type = type;
            Timestamp = DateTime.Now;
        }
    }

    public bool IsApiKeyApproved() => isApiKeyApproved;
    public bool IsSendingRequest() => isSendingRequest;
}