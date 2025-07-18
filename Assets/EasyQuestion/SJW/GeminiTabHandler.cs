using UnityEditor;
using UnityEngine;
using System.Net.Http;
using System.Threading.Tasks;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

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
    private bool _isSendingRequest = false;
    private string _apiStatus = "API 키를 입력하고 '승인'을 눌러주세요.";

    private string _tempImagePath = "";
    private Texture2D _selectedImageTexture;

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
        UpdateApiStatus();
    }

    public void OnGUI(float editorWindowWidth, float editorWindowHeight)
    {
        EditorGUILayout.LabelField("✨ Gemini API 설정", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Google Gemini API Key", EditorStyles.boldLabel);
        _apiKey = EditorGUILayout.PasswordField("API 키:", _apiKey);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("승인", GUILayout.Height(30)))
        {
            EditorPrefs.SetString(GeminiApiKeyPrefKey, _apiKey);
            UpdateApiStatus();
        }
        if (GUILayout.Button("API 키 초기화", GUILayout.Height(30)))
        {
            _apiKey = "";
            EditorPrefs.DeleteKey(GeminiApiKeyPrefKey);
            UpdateApiStatus();
        }
        if (GUILayout.Button("API 키 받으러 가기", GUILayout.Height(30)))
        {
            Application.OpenURL("https://aistudio.google.com/app/apikey");
        }
        EditorGUILayout.EndHorizontal();
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
            _tempImagePath = "";
            _selectedImageTexture = null;
        }
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(10);

        // Height for the top fixed sections
        float topFixedSectionHeight =
            (EditorGUIUtility.singleLineHeight * 2 + 10) + // API Key section
            (30 + 10) + // Buttons
            40 + // HelpBox for API status
            (EditorGUIUtility.singleLineHeight * 2 + 10) + // Model Selection
            40; // HelpBox for model selection

        // Height of the image selection UI block
        float imageUIBlockHeight =
            25 + // Image file select/remove buttons
            EditorGUIUtility.singleLineHeight + // HelpBox
            100 + // Image preview
            5 + // Space after helpbox
            5 + // Space after preview
            10; // Additional padding

        // Height of the "질문하기" section content
        float askQuestionSectionContentHeight =
            EditorGUIUtility.singleLineHeight + 5 + // "질문하기" label
            imageUIBlockHeight + // Image UI block
            60 + // Text area
            40 + 10; // Buttons + space

        // Total bottom space, increased to prevent clipping
        float totalGuaranteedBottomSpace = askQuestionSectionContentHeight + 80;

        float chatScrollViewHeight = editorWindowHeight - topFixedSectionHeight - totalGuaranteedBottomSpace;

        if (chatScrollViewHeight < 150)
        {
            chatScrollViewHeight = 150;
        }
        if (chatScrollViewHeight > 300)
        {
            chatScrollViewHeight = 300;
        }

        EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.ExpandWidth(true));
        EditorGUILayout.LabelField("채팅", EditorStyles.boldLabel);

        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.Height(chatScrollViewHeight));

        foreach (var message in _messages)
        {
            GUIStyle style = new GUIStyle(EditorStyles.wordWrappedLabel);
            style.richText = true;
            if (message.Type == MemoEntry.MessageType.User)
            {
                style.normal.textColor = Color.white;
                EditorGUILayout.SelectableLabel($"<b>[나]</b> {message.Content}", style);
            }
            else
            {
                style.normal.textColor = new Color(0.7f, 0.8f, 1.0f);
                EditorGUILayout.SelectableLabel($"<b>[Gemini]</b> {message.Content}", style);
            }
            EditorGUILayout.Space(5);
        }

        if (_isSendingRequest)
        {
            EditorGUILayout.HelpBox("Gemini 응답 대기 중...", MessageType.Info);
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(20);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.ExpandWidth(true));
        EditorGUILayout.LabelField("질문하기", EditorStyles.boldLabel);

        // Always show image UI, but enable functionality only for gemini-pro-vision
        bool isVisionModel = (_geminiModels[_selectedGeminiModelIndex] == "gemini-pro-vision");
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("이미지 파일 선택", GUILayout.Height(25), GUILayout.Width(150)))
        {
            string path = EditorUtility.OpenFilePanel("이미지 선택", "", "png,jpg,jpeg");
            if (!string.IsNullOrEmpty(path))
            {
                _tempImagePath = path;
                LoadImageTexture(_tempImagePath);
            }
        }
        if (_selectedImageTexture != null)
        {
            if (GUILayout.Button("이미지 제거", GUILayout.Height(25), GUILayout.Width(80)))
            {
                _tempImagePath = "";
                _selectedImageTexture = null;
            }
        }
        EditorGUILayout.EndHorizontal();

        if (_selectedImageTexture != null)
        {
            EditorGUILayout.HelpBox($"선택된 이미지: {Path.GetFileName(_tempImagePath)}", MessageType.Info);
            GUILayout.Label(_selectedImageTexture, GUILayout.Width(100), GUILayout.Height(100));
        }
        else if (!string.IsNullOrEmpty(_tempImagePath))
        {
            EditorGUILayout.HelpBox($"선택된 이미지: {Path.GetFileName(_tempImagePath)} (로딩 실패 또는 유효하지 않은 파일)", MessageType.Warning);
        }
        else
        {
            EditorGUILayout.HelpBox("텍스트와 함께 전송할 이미지를 선택하세요. (선택 사항)", MessageType.Info);
        }
        EditorGUILayout.Space(5);

        _inputPrompt = EditorGUILayout.TextArea(_inputPrompt, GUILayout.MinHeight(60));

        EditorGUILayout.BeginHorizontal();
        bool canSend = IsApiKeyApproved() && !_isSendingRequest;
        if (isVisionModel)
        {
            canSend = canSend && (!string.IsNullOrEmpty(_inputPrompt) || !string.IsNullOrEmpty(_tempImagePath));
        }
        else
        {
            canSend = canSend && !string.IsNullOrEmpty(_inputPrompt);
        }

        GUI.enabled = canSend;
        if (GUILayout.Button("전송", GUILayout.Height(40)))
        {
            SendGeminiQuery(_inputPrompt);
        }
        GUI.enabled = !_isSendingRequest;
        if (GUILayout.Button("채팅 초기화", GUILayout.Height(40), GUILayout.Width(100)))
        {
            _messages.Clear();
            _inputPrompt = "";
            _tempImagePath = "";
            _selectedImageTexture = null;
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
            _apiStatus = $"API 키 승인됨. 모델: {_geminiModels[_selectedGeminiModelIndex]}";
        }
        _parentWindow?.Repaint();
    }

    private bool IsApiKeyApproved()
    {
        return !string.IsNullOrEmpty(_apiKey);
    }

    public async void SendGeminiQuery(string prompt)
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            EditorUtility.DisplayDialog("경고", "Gemini API 키가 설정되지 않았습니다.", "확인");
            return;
        }

        bool isVisionModel = (_geminiModels[_selectedGeminiModelIndex] == "gemini-pro-vision");

        if (string.IsNullOrEmpty(prompt) && (_selectedImageTexture == null || !isVisionModel))
        {
            EditorUtility.DisplayDialog("경고", "질문 내용을 입력해주세요. (이미지 모델은 이미지도 선택 가능)", "확인");
            return;
        }

        if (isVisionModel && string.IsNullOrEmpty(prompt) && _selectedImageTexture == null)
        {
            EditorUtility.DisplayDialog("경고", "이미지 모델을 사용하는 경우, 질문 내용이나 이미지를 선택해야 합니다.", "확인");
            return;
        }

        // Add user message to history (except for statistics analysis)
        if (_parentWindow?.GetStatisticsTabHandler().IsAIAnalysisInProgress() == false)
        {
            _messages.Add(new MemoEntry(prompt, MemoEntry.MessageType.User));
        }

        _isSendingRequest = true;
        _parentWindow?.Repaint();

        string modelName = _geminiModels[_selectedGeminiModelIndex];
        string apiUrl = $"https://generativelanguage.googleapis.com/v1beta/models/{modelName}:generateContent?key={_apiKey}";

        try
        {
            using (HttpClient client = new HttpClient())
            {
                GeminiRequest geminiRequest = new GeminiRequest();
                geminiRequest.contents = new List<GeminiContent>();

                // Add message history (text only, to avoid inline_data issues)
                if (_parentWindow?.GetStatisticsTabHandler().IsAIAnalysisInProgress() == false)
                {
                    foreach (var msg in _messages.Where(m => m.Type == MemoEntry.MessageType.User || m.Type == MemoEntry.MessageType.AI))
                    {
                        geminiRequest.contents.Add(new GeminiContent
                        {
                            role = msg.Type == MemoEntry.MessageType.User ? "user" : "model",
                            parts = new List<GeminiPart> { new GeminiPart { text = msg.Content } }
                        });
                    }
                }

                // Current message
                List<GeminiPart> currentParts = new List<GeminiPart>();
                if (!string.IsNullOrEmpty(prompt))
                {
                    currentParts.Add(new GeminiPart { text = prompt });
                }

                // Add image data as a separate part for vision model
                if (isVisionModel && _selectedImageTexture != null)
                {
                    byte[] imageBytes = File.ReadAllBytes(_tempImagePath);
                    string base64Image = Convert.ToBase64String(imageBytes);
                    currentParts.Add(new GeminiPart { inline_data = new GeminiInlineData { mime_type = GetMimeType(_tempImagePath), data = base64Image } });
                }

                geminiRequest.contents.Add(new GeminiContent { role = "user", parts = currentParts });

                string jsonPayload = JsonUtility.ToJson(geminiRequest);
                StringContent content = new StringContent(jsonPayload, System.Text.Encoding.UTF8, "application/json");

                HttpResponseMessage response = await client.PostAsync(apiUrl, content);
                string responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    GeminiResponse geminiResponse = JsonUtility.FromJson<GeminiResponse>(responseBody);
                    if (geminiResponse != null && geminiResponse.candidates != null && geminiResponse.candidates.Length > 0 && geminiResponse.candidates[0].content.parts != null && geminiResponse.candidates[0].content.parts.Count > 0)
                    {
                        string aiResponse = geminiResponse.candidates[0].content.parts[0].text;
                        _messages.Add(new MemoEntry(aiResponse, MemoEntry.MessageType.AI));
                        _parentWindow.GetQuestionListTabHandler().AddQuestion(prompt, aiResponse, AiServiceType.Gemini);
                        _parentWindow.GetStatisticsTabHandler().RecordKeyword(prompt);
                        _parentWindow.GetStatisticsTabHandler().RecordKeyword(aiResponse);
                    }
                    else
                    {
                        string errorMessage = $"Gemini 응답에 오류가 있습니다. 응답: {responseBody}";
                        Debug.LogError(errorMessage);
                        if (_messages.Any() && _messages[_messages.Count - 1].Content == "응답 대기 중...")
                        {
                            _messages[_messages.Count - 1].Content = errorMessage;
                            _messages[_messages.Count - 1].Timestamp = DateTime.Now;
                        }
                        else
                        {
                            _messages.Add(new MemoEntry(errorMessage, MemoEntry.MessageType.Error));
                        }
                    }
                }
                else
                {
                    string errorMessage = $"Gemini API 요청 실패: {response.StatusCode} - {responseBody}";
                    Debug.LogError(errorMessage);
                    if (_messages.Any() && _messages[_messages.Count - 1].Content == "응답 대기 중...")
                    {
                        _messages[_messages.Count - 1].Content = errorMessage;
                        _messages[_messages.Count - 1].Timestamp = DateTime.Now;
                    }
                    else
                    {
                        _messages.Add(new MemoEntry(errorMessage, MemoEntry.MessageType.Error));
                    }
                }
            }
        }
        catch (HttpRequestException e)
        {
            string errorMessage = $"네트워크 오류 또는 API 통신 문제: {e.Message}";
            Debug.LogError(errorMessage);
            if (_messages.Any() && _messages[_messages.Count - 1].Content == "응답 대기 중...")
            {
                _messages[_messages.Count - 1].Content = errorMessage;
                _messages[_messages.Count - 1].Timestamp = DateTime.Now;
            }
            else
            {
                _messages.Add(new MemoEntry(errorMessage, MemoEntry.MessageType.Error));
            }
        }
        catch (Exception e)
        {
            string errorMessage = $"예상치 못한 오류 발생: {e.Message}";
            Debug.LogError(errorMessage);
            if (_messages.Any() && _messages[_messages.Count - 1].Content == "응답 대기 중...")
            {
                _messages[_messages.Count - 1].Content = errorMessage;
                _messages[_messages.Count - 1].Timestamp = DateTime.Now;
            }
            else
            {
                _messages.Add(new MemoEntry(errorMessage, MemoEntry.MessageType.Error));
            }
        }
        finally
        {
            _inputPrompt = "";
            _tempImagePath = "";
            _selectedImageTexture = null;
            _isSendingRequest = false;
            _parentWindow?.Repaint();
            _scrollPos.y = Mathf.Infinity;
            if (_parentWindow?.GetStatisticsTabHandler().IsAIAnalysisInProgress() == true)
            {
                _parentWindow.GetStatisticsTabHandler().SetAIAnalysisInProgress(false);
            }
        }
    }

    private void LoadImageTexture(string path)
    {
        if (File.Exists(path))
        {
            byte[] fileData = File.ReadAllBytes(path);
            _selectedImageTexture = new Texture2D(2, 2);
            _selectedImageTexture.LoadImage(fileData);
        }
        else
        {
            _selectedImageTexture = null;
            Debug.LogError("Failed to load image: " + path);
        }
    }

    private string GetMimeType(string filePath)
    {
        string extension = Path.GetExtension(filePath).ToLower();
        switch (extension)
        {
            case ".png": return "image/png";
            case ".jpg":
            case ".jpeg": return "image/jpeg";
            case ".gif": return "image/gif";
            case ".webp": return "image/webp";
            default: return "application/octet-stream";
        }
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
        public GeminiInlineData inline_data;
    }

    [System.Serializable]
    public class GeminiInlineData
    {
        public string mime_type;
        public string data;
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
        public string finishReason;
        public int index;
    }
}