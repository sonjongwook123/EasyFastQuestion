using UnityEditor;
using UnityEngine;
using System.Net.Http;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public enum GeminiModel
{
    Gemini15Pro,
    Gemini15Flash,
    GeminiProVision
}

[System.Serializable]
public class GeminiTabHandler
{
    private GeminiChatGPTIntegrationEditor _parentWindow;
    private string _apiKey = "";
    private string _inputPrompt = "";
    private Vector2 _scrollPos;
    private List<MemoEntry> _messages = new List<MemoEntry>();
    private string _chatDisplayText = "";
    private bool _isSendingRequest = false;
    private string _apiStatus = "API 키를 입력하고 '승인'을 눌러주세요.";
    private bool _isApiKeyEditable = true; 

    private int _selectedGeminiModelIndex = 0;
    private string[] _geminiModels = { "gemini-1.5-pro", "gemini-1.5-flash", "gemini-pro-vision" };

    private const string GeminiApiKeyPrefKey = "GeminiApiKey";
    private const string SelectedGeminiModelPrefKey = "SelectedGeminiModel";

    public GeminiTabHandler() { }

    public void Initialize(EditorWindow parentWindow)
    {
        _parentWindow = parentWindow as GeminiChatGPTIntegrationEditor;
        _apiKey = EditorPrefs.GetString(GeminiApiKeyPrefKey, "");
        _selectedGeminiModelIndex = EditorPrefs.GetInt(SelectedGeminiModelPrefKey, 0);
        _isApiKeyEditable = string.IsNullOrEmpty(_apiKey); 
        UpdateApiStatus();
        UpdateChatDisplayText();
    }

    public string GetApiKey() 
    {
        return _apiKey;
    }

    public string GetSelectedModel()
    {
        return _geminiModels[_selectedGeminiModelIndex];
    }

    public void OnGUI(float editorWindowWidth, float editorWindowHeight)
    {
        EditorGUILayout.LabelField("✨ Gemini API 설정", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Google Gemini API Key", EditorStyles.boldLabel);
        GUI.enabled = _isApiKeyEditable;
        _apiKey = EditorGUILayout.PasswordField("API 키:", _apiKey);
        GUI.enabled = true;

        EditorGUILayout.BeginHorizontal();

        GUI.enabled = _isApiKeyEditable && !string.IsNullOrEmpty(_apiKey);
        if (GUILayout.Button("승인", GUILayout.Height(30)))
        {
            EditorPrefs.SetString(GeminiApiKeyPrefKey, _apiKey);
            _isApiKeyEditable = false; 
            UpdateApiStatus();
        }

        GUI.enabled = !_isApiKeyEditable && IsApiKeyApproved();
        if (GUILayout.Button("수정", GUILayout.Height(30)))
        {
            _isApiKeyEditable = true;
            _parentWindow?.Repaint();
        }

        GUI.enabled = !string.IsNullOrEmpty(_apiKey);
        if (GUILayout.Button("API 키 초기화", GUILayout.Height(30)))
        {
            _apiKey = "";
            _isApiKeyEditable = true;
            EditorPrefs.DeleteKey(GeminiApiKeyPrefKey);
            UpdateApiStatus();
        }

        GUI.enabled = true;
        if (GUILayout.Button("API 키 받으러 가기", GUILayout.Height(30)))
        {
            Application.OpenURL("https://aistudio.google.com/app/apikey");
        }
        EditorGUILayout.EndHorizontal();
        GUI.enabled = true; 

        EditorGUILayout.HelpBox(_apiStatus, IsApiKeyApproved() ? MessageType.Info : MessageType.Warning);
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(10);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Gemini 모델 선택", EditorStyles.boldLabel);
        int newGeminiModelIndex = EditorGUILayout.Popup("모델:", _selectedGeminiModelIndex, _geminiModels);
        if (newGeminiModelIndex != _selectedGeminiModelIndex)
        {
            _selectedGeminiModelIndex = newGeminiModelIndex;
            EditorPrefs.SetInt(SelectedGeminiModelPrefKey, _selectedGeminiModelIndex);
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
            EditorGUILayout.HelpBox("Gemini 응답 대기 중...", MessageType.Info);
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
            SendGeminiQuery(_inputPrompt);
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
        return !string.IsNullOrEmpty(_apiKey) && !_isApiKeyEditable;
    }

    private async void SendGeminiQuery(string userPrompt, bool isAnalysisRequest = false)
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            Debug.LogError("Gemini API 키가 설정되지 않았습니다.");
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
            string apiUrl = $"https://generativelanguage.googleapis.com/v1beta/models/{_geminiModels[_selectedGeminiModelIndex]}:generateContent?key={_apiKey}";
            client.Timeout = TimeSpan.FromSeconds(120); // Set timeout to 120 seconds

            var requestBody = new GeminiRequest
            {
                contents = _messages.Select(m => new GeminiContent
                {
                    role = m.Type == MemoEntry.MessageType.User ? "user" : "model",
                    parts = new List<GeminiPart> { new GeminiPart { text = m.Content } }
                }).ToList()
            };

            string jsonRequestBody = JsonUtility.ToJson(requestBody);
            Debug.Log($"Gemini Request: {jsonRequestBody}");
            try
            {
                StringContent content = new StringContent(jsonRequestBody, Encoding.UTF8, "application/json");
                HttpResponseMessage response = await client.PostAsync(apiUrl, content);

                if (response.IsSuccessStatusCode)
                {
                    string jsonResponse = await response.Content.ReadAsStringAsync();
                    Debug.Log($"Gemini Response: {jsonResponse}");
                    GeminiResponse geminiResponse = JsonUtility.FromJson<GeminiResponse>(jsonResponse);

                    if (geminiResponse != null && geminiResponse.candidates != null && geminiResponse.candidates.Length > 0)
                    {
                        string aiResponse = geminiResponse.candidates[0].content.parts[0].text;
                        _messages.Add(new MemoEntry(aiResponse, MemoEntry.MessageType.AI));
                        UpdateChatDisplayText();
                        if (!isAnalysisRequest)
                        {
                            _parentWindow?.GetQuestionListTabHandler().AddQuestion(userPrompt, aiResponse, AiServiceType.Gemini);
                        }
                    }
                    else
                    {
                        string errorMessage = "Gemini 응답 형식이 올바르지 않습니다.";
                        Debug.LogError(errorMessage);
                        _messages.Add(new MemoEntry(errorMessage, MemoEntry.MessageType.Error));
                        UpdateChatDisplayText();
                    }
                }
                else
                {
                    string errorContent = await response.Content.ReadAsStringAsync();
                    string errorMessage = $"Gemini API 오류: {response.StatusCode} - {errorContent}";
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
                _scrollPos.y = Mathf.Infinity;
                SetAIAnalysisInProgress(false);
            }
        }
    }

    // New Public Method for Analysis Requests
    public async Task<string> SendGeminiAnalysisRequest(string prompt)
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            Debug.LogError("Gemini API 키가 설정되지 않았습니다. 분석을 수행할 수 없습니다.");
            return "Gemini API 키가 설정되지 않았습니다.";
        }

        using (HttpClient client = new HttpClient())
        {
            string apiUrl = $"https://generativelanguage.googleapis.com/v1beta/models/{_geminiModels[_selectedGeminiModelIndex]}:generateContent?key={_apiKey}";
            client.Timeout = TimeSpan.FromSeconds(120);

            var requestBody = new GeminiRequest
            {
                contents = new List<GeminiContent>
                {
                    new GeminiContent
                    {
                        role = "user",
                        parts = new List<GeminiPart> { new GeminiPart { text = prompt } }
                    }
                }
            };

            string jsonRequestBody = JsonUtility.ToJson(requestBody);
            try
            {
                StringContent content = new StringContent(jsonRequestBody, Encoding.UTF8, "application/json");
                HttpResponseMessage response = await client.PostAsync(apiUrl, content);

                if (response.IsSuccessStatusCode)
                {
                    string jsonResponse = await response.Content.ReadAsStringAsync();
                    GeminiResponse geminiResponse = JsonUtility.FromJson<GeminiResponse>(jsonResponse);
                    if (geminiResponse != null && geminiResponse.candidates != null && geminiResponse.candidates.Length > 0)
                    {
                        return geminiResponse.candidates[0].content.parts[0].text;
                    }
                    else
                    {
                        return "Gemini 응답 형식이 올바르지 않습니다.";
                    }
                }
                else
                {
                    string errorContent = await response.Content.ReadAsStringAsync();
                    return $"Gemini API 오류: {response.StatusCode} - {errorContent}";
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
            _isApiKeyEditable = true;
        }
        else if (_isApiKeyEditable)
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
                    builder.AppendLine($"<color=#AA33FF><b>[Gemini]</b></color> {formattedContent}");
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
    public class GeminiRequest
    {
        public List<GeminiContent> contents;
    }

    [System.Serializable]
    public class GeminiContent
    {
        public string role;
        public List<GeminiPart> parts;
    }

    [System.Serializable]
    public class GeminiPart
    {
        public string text;
    }

    [System.Serializable]
    public class GeminiResponse
    {
        public GeminiCandidate[] candidates;
    }

    [System.Serializable]
    public class GeminiCandidate
    {
        public GeminiContent content;
        public string finish_reason;
        public int index;
    }
}