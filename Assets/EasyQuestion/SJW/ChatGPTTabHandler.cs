using UnityEditor;
using UnityEngine;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System;

[System.Serializable]
public class ChatGPTTabHandler
{
    private GeminiChatGPTIntegrationEditor _parentWindow;
    private string _apiKey = "";
    private string _inputPrompt = "";
    private Vector2 _scrollPos;
    private List<MemoEntry> _messages = new List<MemoEntry>();
    private string _chatDisplayText = "";
    private bool _isSendingRequest = false;
    private string _apiStatus = "API 키를 입력하고 '승인'을 눌러주세요.";
    private int _selectedModelIndex = 0;
    private string[] _chatGPTModels = {
        "gpt-3.5-turbo",
        "gpt-4o",
        "gpt-4-turbo",
    };
    private bool _isApiKeyEditableChatGPT = true; // Flag for API key editability

    private const string ChatGPTApiKeyPrefKey = "ChatGPTApiKey";
    private const string ChatGPTModelPrefKey = "ChatGPTSelectedModel";

    public ChatGPTTabHandler() { }

    public void Initialize(EditorWindow parentWindow)
    {
        _parentWindow = parentWindow as GeminiChatGPTIntegrationEditor;
        _apiKey = EditorPrefs.GetString(ChatGPTApiKeyPrefKey, "");
        _selectedModelIndex = EditorPrefs.GetInt(ChatGPTModelPrefKey, 0);
        _isApiKeyEditableChatGPT = string.IsNullOrEmpty(_apiKey); // Initialize editability based on saved key
        UpdateApiStatus();
        UpdateChatDisplayText();
    }

    public void OnGUI(float editorWindowWidth, float editorWindowHeight)
    {
        EditorGUILayout.LabelField("🤖 ChatGPT API 설정", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("OpenAI API Key", EditorStyles.boldLabel);

        GUI.enabled = _isApiKeyEditableChatGPT; // Control editability of the API key field
        _apiKey = EditorGUILayout.PasswordField("API 키:", _apiKey);
        GUI.enabled = true; // Re-enable GUI for buttons and other elements

        EditorGUILayout.BeginHorizontal();

        // "승인" (Approve) button: Only enabled if editable and API key is not empty
        GUI.enabled = _isApiKeyEditableChatGPT && !string.IsNullOrEmpty(_apiKey);
        if (GUILayout.Button("승인", GUILayout.Height(30)))
        {
            EditorPrefs.SetString(ChatGPTApiKeyPrefKey, _apiKey);
            UpdateApiStatus();
            _isApiKeyEditableChatGPT = false; // Lock editing after approval
        }

        // "수정" (Edit) button: Only enabled if not editable and API key is approved
        GUI.enabled = !_isApiKeyEditableChatGPT && IsApiKeyApproved();
        if (GUILayout.Button("수정", GUILayout.Height(30)))
        {
            _isApiKeyEditableChatGPT = true; // Unlock for editing
        }

        // "API 키 초기화" (Reset API Key) button: Always enabled if an API key exists
        GUI.enabled = !string.IsNullOrEmpty(_apiKey);
        if (GUILayout.Button("API 키 초기화", GUILayout.Height(30)))
        {
            _apiKey = "";
            EditorPrefs.DeleteKey(ChatGPTApiKeyPrefKey);
            UpdateApiStatus();
            _isApiKeyEditableChatGPT = true; // Enable editing after reset
        }

        // "API 키 받으러 가기" (Go to Get API Key) button: Always enabled
        GUI.enabled = true;
        if (GUILayout.Button("API 키 받으러 가기", GUILayout.Height(30)))
        {
            Application.OpenURL("https://platform.openai.com/account/api-keys");
        }
        EditorGUILayout.EndHorizontal();
        GUI.enabled = true; // Ensure GUI is fully re-enabled after buttons

        EditorGUILayout.HelpBox(_apiStatus, IsApiKeyApproved() ? MessageType.Info : MessageType.Warning);
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(10);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("ChatGPT 모델 선택", EditorStyles.boldLabel);
        int newModelIndex = EditorGUILayout.Popup("모델:", _selectedModelIndex, _chatGPTModels);
        if (newModelIndex != _selectedModelIndex)
        {
            _selectedModelIndex = newModelIndex;
            EditorPrefs.SetInt(ChatGPTModelPrefKey, _selectedModelIndex);
            _messages.Clear();
            _inputPrompt = "";
            UpdateChatDisplayText();
        }
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(10);

        // Height for the top fixed sections
        float topFixedSectionHeight =
            (EditorGUIUtility.singleLineHeight * 2 + 10) + // API Key label + password field
            (30 + 10) + // Buttons (approx height + space)
            40 + // HelpBox for API status
            (EditorGUIUtility.singleLineHeight * 2 + 10) + // Model Selection
            40; // HelpBox for model selection

        // Height of the "질문하기" section content
        float askQuestionSectionContentHeight =
            EditorGUIUtility.singleLineHeight + 5 + // "질문하기" label
            60 + // Text area
            40 + 10; // Buttons + space

        // Total bottom space, increased to prevent clipping
        float totalGuaranteedBottomSpace = askQuestionSectionContentHeight + 80;

        float chatScrollViewHeight = editorWindowHeight - topFixedSectionHeight - totalGuaranteedBottomSpace;

        // --- 변경된 부분 시작 ---
        float minChatViewHeight = 150f; 
        float maxChatViewHeight = editorWindowHeight * 0.4f;

        if (chatScrollViewHeight < minChatViewHeight)
        {
            chatScrollViewHeight = minChatViewHeight;
        }
        if (chatScrollViewHeight > maxChatViewHeight)
        {
            chatScrollViewHeight = maxChatViewHeight;
        }
        // --- 변경된 부분 끝 ---

        EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.ExpandWidth(true));
        EditorGUILayout.LabelField("채팅", EditorStyles.boldLabel);

        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.Height(chatScrollViewHeight));
        GUIStyle chatStyle = new GUIStyle(EditorStyles.textArea);
        chatStyle.richText = true;

        // --- 변경된 부분 시작 ---
        // _chatDisplayText의 실제 높이를 대략적으로 계산합니다.
        // 스크롤뷰 내의 너비를 고려하여 계산합니다. (약간의 패딩 제외)
        //float estimatedTextHeight = chatStyle.CalcHeight(new GUIContent(_chatDisplayText), editorWindowWidth - 60); 
        EditorGUILayout.SelectableLabel(_chatDisplayText, chatStyle, GUILayout.MinHeight(450));
        // GUILayout.ExpandHeight(true)는 제거합니다.
        // --- 변경된 부분 끝 ---

        EditorGUILayout.EndScrollView();

        if (_isSendingRequest)
        {
            EditorGUILayout.HelpBox("ChatGPT 응답 대기 중...", MessageType.Info);
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(20);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.ExpandWidth(true));
        EditorGUILayout.LabelField("질문하기", EditorStyles.boldLabel);

        _inputPrompt = EditorGUILayout.TextArea(_inputPrompt, GUILayout.MinHeight(60));

        EditorGUILayout.BeginHorizontal();
        bool canSend = IsApiKeyApproved() && !_isSendingRequest && !string.IsNullOrEmpty(_inputPrompt);

        GUI.enabled = canSend;
        if (GUILayout.Button("전송", GUILayout.Height(40)))
        {
            SendChatGPTQuery(_inputPrompt);
        }
        GUI.enabled = !_isSendingRequest;
        if (GUILayout.Button("채팅 초기화", GUILayout.Height(40), GUILayout.Width(100)))
        {
            _messages.Clear();
            _inputPrompt = "";
            UpdateChatDisplayText();
        }
        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(20);
    }

    private void UpdateApiStatus()
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            _apiStatus = "API 키가 입력되지 않았습니다.";
        }
        else
        {
            _apiStatus = $"API 키 승인됨. 모델: {_chatGPTModels[_selectedModelIndex]}";
        }
        _parentWindow?.Repaint();
    }

    private void UpdateChatDisplayText()
    {
        StringBuilder chatText = new StringBuilder();
        foreach (var message in _messages)
        {
            switch (message.Type)
            {
                case MemoEntry.MessageType.User:
                    chatText.AppendLine($"<color=#3366FF><b>[나]</b></color> {message.Content}");
                    break;
                case MemoEntry.MessageType.AI:
                    chatText.AppendLine($"<color=#008000><b>[ChatGPT]</b></color> {message.Content}");
                    break;
                case MemoEntry.MessageType.Error:
                    chatText.AppendLine($"<color=red><b>[오류]</b></color> {message.Content}");
                    break;
                case MemoEntry.MessageType.Info:
                    chatText.AppendLine($"<color=grey><b>[정보]</b></color> {message.Content}");
                    break;
                case MemoEntry.MessageType.Warning:
                    chatText.AppendLine($"<color=orange><b>[경고]</b></color> {message.Content}");
                    break;
                default:
                    chatText.AppendLine($"{message.Content}");
                    break;
            }
            chatText.AppendLine(); // Add an empty line after each message for spacing
        }
        _chatDisplayText = chatText.ToString();
        _parentWindow?.Repaint();
        _scrollPos.y = Mathf.Infinity; // Ensure scroll to bottom after updating display
    }

    private bool IsApiKeyApproved()
    {
        return !string.IsNullOrEmpty(_apiKey);
    }

    public async void SendChatGPTQuery(string prompt)
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            EditorUtility.DisplayDialog("경고", "ChatGPT API 키가 설정되지 않았습니다.", "확인");
            return;
        }

        if (string.IsNullOrEmpty(prompt))
        {
            EditorUtility.DisplayDialog("경고", "질문 내용을 입력해주세요.", "확인");
            return;
        }

        // Add user message to history (except for statistics analysis)
        if (_parentWindow?.GetStatisticsTabHandler().IsAIAnalysisInProgress() == false)
        {
            _messages.Add(new MemoEntry(prompt, MemoEntry.MessageType.User));
            UpdateChatDisplayText();
        }

        _isSendingRequest = true;
        _parentWindow?.Repaint();

        string model = _chatGPTModels[_selectedModelIndex];
        string apiUrl = "https://api.openai.com/v1/chat/completions";

        try
        {
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Authorization", "Bearer " + _apiKey);

                var requestMessages = new List<ChatGPTRequestMessage>();

                if (_parentWindow?.GetStatisticsTabHandler().IsAIAnalysisInProgress() == false)
                {
                    foreach (var msg in _messages.Where(m => m.Type == MemoEntry.MessageType.User || m.Type == MemoEntry.MessageType.AI))
                    {
                        requestMessages.Add(new ChatGPTRequestMessage(msg.Content, msg.Type == MemoEntry.MessageType.User ? "user" : "assistant"));
                    }
                }

                requestMessages.Add(new ChatGPTRequestMessage(prompt, "user"));

                ChatGPTRequest chatGPTRequest = new ChatGPTRequest
                {
                    model = model,
                    messages = requestMessages.ToArray()
                };

                string jsonPayload = JsonUtility.ToJson(chatGPTRequest);
                StringContent content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                HttpResponseMessage response = await client.PostAsync(apiUrl, content);
                string responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    ChatGPTResponse chatGPTResponse = JsonUtility.FromJson<ChatGPTResponse>(responseBody);
                    if (chatGPTResponse != null && chatGPTResponse.choices != null && chatGPTResponse.choices.Length > 0)
                    {
                        string aiResponse = chatGPTResponse.choices[0].message.content;
                        _messages.Add(new MemoEntry(aiResponse, MemoEntry.MessageType.AI));
                        _parentWindow.GetQuestionListTabHandler().AddQuestion(prompt, aiResponse, AiServiceType.ChatGPT);
                        _parentWindow.GetStatisticsTabHandler().RecordKeyword(prompt);
                        _parentWindow.GetStatisticsTabHandler().RecordKeyword(aiResponse);
                        UpdateChatDisplayText();
                    }
                    else
                    {
                        string errorMessage = $"ChatGPT 응답에 오류가 있습니다. 응답: {responseBody}";
                        Debug.LogError(errorMessage);
                        _messages.Add(new MemoEntry(errorMessage, MemoEntry.MessageType.Error));
                        UpdateChatDisplayText();
                    }
                }
                else
                {
                    string errorMessage = $"ChatGPT API 요청 실패: {response.StatusCode} - {responseBody}";
                    Debug.LogError(errorMessage);
                    _messages.Add(new MemoEntry(errorMessage, MemoEntry.MessageType.Error));
                    UpdateChatDisplayText();
                }
            }
        }
        catch (HttpRequestException e)
        {
            string errorMessage = $"네트워크 오류 또는 API 통신 문제: {e.Message}";
            Debug.LogError(errorMessage);
            _messages.Add(new MemoEntry(errorMessage, MemoEntry.MessageType.Error));
            UpdateChatDisplayText();
        }
        catch (Exception e)
        {
            string errorMessage = $"예상치 못한 오류 발생: {e.Message}";
            Debug.LogError(errorMessage);
            _messages.Add(new MemoEntry(errorMessage, MemoEntry.MessageType.Error));
            UpdateChatDisplayText();
        }
        finally
        {
            _inputPrompt = "";
            _isSendingRequest = false;
            _parentWindow?.Repaint();
            _scrollPos.y = Mathf.Infinity; // Scroll to the bottom after sending a request
            if (_parentWindow?.GetStatisticsTabHandler().IsAIAnalysisInProgress() == true)
            {
                _parentWindow.GetStatisticsTabHandler().SetAIAnalysisInProgress(false);
            }
        }
    }

    [System.Serializable]
    private class ChatGPTRequest
    {
        public string model;
        public ChatGPTRequestMessage[] messages;
        public float temperature = 0.7f;
        public int max_tokens = 2000;
    }

    [System.Serializable]
    private class ChatGPTRequestMessage
    {
        public string role;
        public string content;

        public ChatGPTRequestMessage(string content, string role)
        {
            this.content = content;
            this.role = role;
        }
    }

    [System.Serializable]
    private class ChatGPTResponse
    {
        public Choice[] choices;
    }

    [System.Serializable]
    private class Choice
    {
        public ChatGPTMessage message;
        public string finish_reason;
        public int index;
    }

    [System.Serializable]
    private class ChatGPTMessage
    {
        public string role;
        public string content;
    }
}