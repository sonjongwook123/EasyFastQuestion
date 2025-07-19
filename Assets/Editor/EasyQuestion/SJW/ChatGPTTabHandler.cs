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
    private bool _isApiKeyEditableChatGPT = true;

    private const string ChatGPTApiKeyPrefKey = "ChatGPTApiKey";
    private const string ChatGPTModelPrefKey = "ChatGPTSelectedModel";

    public ChatGPTTabHandler() { }

    public void Initialize(EditorWindow parentWindow)
    {
        _parentWindow = parentWindow as GeminiChatGPTIntegrationEditor;
        _apiKey = EditorPrefs.GetString(ChatGPTApiKeyPrefKey, "");
        _selectedModelIndex = EditorPrefs.GetInt(ChatGPTModelPrefKey, 0);
        _isApiKeyEditableChatGPT = string.IsNullOrEmpty(_apiKey);
        UpdateApiStatus();
        UpdateChatDisplayText();
    }

    public string GetApiKey() 
    {
        return _apiKey;
    }

    public string GetSelectedModel()
    {
        return _chatGPTModels[_selectedModelIndex];
    }

    public void OnGUI(float editorWindowWidth, float editorWindowHeight)
    {
        EditorGUILayout.LabelField("🤖 ChatGPT API 설정", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("OpenAI API Key", EditorStyles.boldLabel);

        GUI.enabled = _isApiKeyEditableChatGPT; 
        _apiKey = EditorGUILayout.PasswordField("API 키:", _apiKey);
        GUI.enabled = true; 

        EditorGUILayout.BeginHorizontal();

        GUI.enabled = _isApiKeyEditableChatGPT && !string.IsNullOrEmpty(_apiKey);
        if (GUILayout.Button("승인", GUILayout.Height(30)))
        {
            EditorPrefs.SetString(ChatGPTApiKeyPrefKey, _apiKey);
            UpdateApiStatus();
            _isApiKeyEditableChatGPT = false;
        }

        GUI.enabled = !_isApiKeyEditableChatGPT && IsApiKeyApproved();
        if (GUILayout.Button("수정", GUILayout.Height(30)))
        {
            _isApiKeyEditableChatGPT = true;
        }

        GUI.enabled = !string.IsNullOrEmpty(_apiKey);
        if (GUILayout.Button("API 키 초기화", GUILayout.Height(30)))
        {
            _apiKey = "";
            EditorPrefs.DeleteKey(ChatGPTApiKeyPrefKey);
            UpdateApiStatus();
            _isApiKeyEditableChatGPT = true;
        }

        GUI.enabled = true;
        if (GUILayout.Button("API 키 받으러 가기", GUILayout.Height(30)))
        {
            Application.OpenURL("https://platform.openai.com/account/api-keys");
        }
        EditorGUILayout.EndHorizontal();
        GUI.enabled = true; 

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

        float topFixedSectionHeight =
            (EditorGUIUtility.singleLineHeight * 2 + 10) + 
            (30 + 10) + 
            40 + 
            (EditorGUIUtility.singleLineHeight * 2 + 10) + 
            40; 

        float askQuestionSectionContentHeight =
            EditorGUIUtility.singleLineHeight + 5 +
            60 +
            40 + 10;

        float totalGuaranteedBottomSpace = askQuestionSectionContentHeight + 80;

        float chatScrollViewHeight = editorWindowHeight - topFixedSectionHeight - totalGuaranteedBottomSpace;
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

        EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.ExpandWidth(true));
        EditorGUILayout.LabelField("채팅", EditorStyles.boldLabel);
        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.Height(chatScrollViewHeight));
        GUIStyle chatStyle = new GUIStyle(EditorStyles.textArea);
        chatStyle.richText = true;
        float estimatedTextHeight = chatStyle.CalcHeight(new GUIContent(_chatDisplayText), editorWindowWidth - 60); 
        EditorGUILayout.SelectableLabel(_chatDisplayText, chatStyle, GUILayout.MinHeight(250+estimatedTextHeight));
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
        GUI.enabled = true;

        GUI.enabled = !_isSendingRequest;
        if (GUILayout.Button("대화 내용 삭제", GUILayout.Height(40)))
        {
            if (EditorUtility.DisplayDialog("대화 내용 삭제", "모든 대화 내용을 정말 삭제하시겠습니까?", "삭제", "취소"))
            {
                _messages.Clear();
                UpdateChatDisplayText();
            }
        }
        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }

    private bool IsApiKeyApproved()
    {
        return !string.IsNullOrEmpty(_apiKey) && !_isApiKeyEditableChatGPT;
    }

    private async void SendChatGPTQuery(string userPrompt, bool isAnalysisRequest = false)
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            Debug.LogError("ChatGPT API 키가 설정되지 않았습니다.");
            return;
        }

        if (_isSendingRequest) return;

        SetAIAnalysisInProgress(true);
        _isSendingRequest = true;
        _parentWindow?.Repaint();

        if (!isAnalysisRequest)
        {
            _messages.Add(new MemoEntry(userPrompt, MemoEntry.MessageType.User));
            UpdateChatDisplayText();
        }

        using (HttpClient client = new HttpClient())
        {
            string apiUrl = "https://api.openai.com/v1/chat/completions";
            client.DefaultRequestHeaders.Add("Authorization", "Bearer " + _apiKey);
            client.Timeout = TimeSpan.FromSeconds(120);

            List<ChatGPTRequestMessage> requestMessages = _messages.Select(m => new ChatGPTRequestMessage(m.Content, m.Type == MemoEntry.MessageType.User ? "user" : "assistant")).ToList();

            var requestBody = new ChatGPTRequest
            {
                model = _chatGPTModels[_selectedModelIndex],
                messages = requestMessages.ToArray(),
                temperature = 0.7f,
                max_tokens = 2000
            };

            string jsonRequestBody = JsonUtility.ToJson(requestBody);
            Debug.Log($"ChatGPT Request: {jsonRequestBody}");
            try
            {
                StringContent content = new StringContent(jsonRequestBody, Encoding.UTF8, "application/json");
                HttpResponseMessage response = await client.PostAsync(apiUrl, content);

                if (response.IsSuccessStatusCode)
                {
                    string jsonResponse = await response.Content.ReadAsStringAsync();
                    Debug.Log($"ChatGPT Response: {jsonResponse}");
                    ChatGPTResponse chatGPTResponse = JsonUtility.FromJson<ChatGPTResponse>(jsonResponse);

                    if (chatGPTResponse != null && chatGPTResponse.choices != null && chatGPTResponse.choices.Length > 0)
                    {
                        string aiResponse = chatGPTResponse.choices[0].message.content;
                        _messages.Add(new MemoEntry(aiResponse, MemoEntry.MessageType.AI));
                        UpdateChatDisplayText();
                        if (!isAnalysisRequest)
                        {
                            _parentWindow?.GetQuestionListTabHandler().AddQuestion(userPrompt, aiResponse, AiServiceType.ChatGPT);
                        }
                    }
                    else
                    {
                        string errorMessage = "ChatGPT 응답 형식이 올바르지 않습니다.";
                        Debug.LogError(errorMessage);
                        _messages.Add(new MemoEntry(errorMessage, MemoEntry.MessageType.Error));
                        UpdateChatDisplayText();
                    }
                }
                else
                {
                    string errorContent = await response.Content.ReadAsStringAsync();
                    string errorMessage = $"ChatGPT API 오류: {response.StatusCode} - {errorContent}";
                    Debug.LogError(errorMessage);
                    _messages.Add(new MemoEntry(errorMessage, MemoEntry.MessageType.Error));
                    UpdateChatDisplayText();
                }
            }
            catch (HttpRequestException e)
            {
                string errorMessage = $"네트워크 오류 또는 API 요청 실패: {e.Message}";
                Debug.LogError(errorMessage);
                _messages.Add(new MemoEntry(errorMessage, MemoEntry.MessageType.Error));
                UpdateChatDisplayText();
            }
            catch (TaskCanceledException e)
            {
                string errorMessage = $"요청 시간 초과: {e.Message}";
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
                SetAIAnalysisInProgress(false);
            }
        }
    }

    // New Public Method for Analysis Requests
    public async Task<string> SendChatGPTAnalysisRequest(string prompt)
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            Debug.LogError("ChatGPT API 키가 설정되지 않았습니다. 분석을 수행할 수 없습니다.");
            return "ChatGPT API 키가 설정되지 않았습니다.";
        }

        using (HttpClient client = new HttpClient())
        {
            string apiUrl = "https://api.openai.com/v1/chat/completions";
            client.DefaultRequestHeaders.Add("Authorization", "Bearer " + _apiKey);
            client.Timeout = TimeSpan.FromSeconds(120);

            var requestBody = new ChatGPTRequest
            {
                model = _chatGPTModels[_selectedModelIndex],
                messages = new ChatGPTRequestMessage[] { new ChatGPTRequestMessage(prompt, "user") },
                temperature = 0.7f,
                max_tokens = 2000
            };

            string jsonRequestBody = JsonUtility.ToJson(requestBody);
            try
            {
                StringContent content = new StringContent(jsonRequestBody, Encoding.UTF8, "application/json");
                HttpResponseMessage response = await client.PostAsync(apiUrl, content);

                if (response.IsSuccessStatusCode)
                {
                    string jsonResponse = await response.Content.ReadAsStringAsync();
                    ChatGPTResponse chatGPTResponse = JsonUtility.FromJson<ChatGPTResponse>(jsonResponse);
                    if (chatGPTResponse != null && chatGPTResponse.choices != null && chatGPTResponse.choices.Length > 0)
                    {
                        return chatGPTResponse.choices[0].message.content;
                    }
                    else
                    {
                        return "ChatGPT 응답 형식이 올바르지 않습니다.";
                    }
                }
                else
                {
                    string errorContent = await response.Content.ReadAsStringAsync();
                    return $"ChatGPT API 오류: {response.StatusCode} - {errorContent}";
                }
            }
            catch (HttpRequestException e)
            {
                return $"네트워크 오류 또는 API 요청 실패: {e.Message}";
            }
            catch (TaskCanceledException e)
            {
                return $"요청 시간 초과: {e.Message}";
            }
            catch (Exception e)
            {
                return $"예상치 못한 오류 발생: {e.Message}";
            }
        }
    }

    private void UpdateApiStatus()
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            _apiStatus = "API 키를 입력하고 '승인'을 눌러주세요.";
            _isApiKeyEditableChatGPT = true;
        }
        else if (_isApiKeyEditableChatGPT)
        {
            _apiStatus = "API 키가 입력되었습니다. '승인'을 눌러 저장하세요.";
        }
        else
        {
            _apiStatus = "API 키가 승인되었습니다.";
        }
        _parentWindow?.Repaint();
    }

    private void UpdateChatDisplayText()
    {
        StringBuilder builder = new StringBuilder();
        foreach (var message in _messages)
        {
            string formattedContent = message.Content;
            switch (message.Type)
            {
                case MemoEntry.MessageType.User:
                    builder.AppendLine($"<color=#3366FF><b>[나]</b></color> {formattedContent}");
                    break;
                case MemoEntry.MessageType.AI:
                    builder.AppendLine($"<color=#AA33FF><b>[ChatGPT]</b></color> {formattedContent}");
                    break;
                case MemoEntry.MessageType.Info:
                    builder.AppendLine($"<color=#3399FF><b>[정보]</b></color> {formattedContent}");
                    break;
                case MemoEntry.MessageType.Warning:
                    builder.AppendLine($"<color=#FFA500><b>[경고]</b></color> {formattedContent}");
                    break;
                case MemoEntry.MessageType.Error:
                    builder.AppendLine($"<color=#FF0000><b>[오류]</b></color> {formattedContent}");
                    break;
                default:
                    builder.AppendLine(formattedContent);
                    break;
            }
            builder.AppendLine();
        }
        _chatDisplayText = builder.ToString();
        _parentWindow?.Repaint();
    }

    private void SetAIAnalysisInProgress(bool inProgress)
    {
        _parentWindow?.GetStatisticsTabHandler().SetAIAnalysisInProgress(inProgress);
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