// Editor/GeminiTabHandler.cs
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

[System.Serializable]
public class GeminiTabHandler
{
    private string geminiQuery = "";
    private Vector2 geminiScrollPos;
    private List<MessageEntry> geminiMessages = new List<MessageEntry>();
    private string geminiApiKey = "";
    private string[] availableGeminiModels = { "gemini-pro", "gemini-1.5-flash", "gemini-1.5-pro" };
    private string geminiAiVersion = "gemini-pro";
    private bool isApiKeyApproved = false;
    private bool isSendingRequest = false;
    private bool isApprovingApiKey = false;
    private bool showModelUnavailableWarning = false;
    private bool showServiceSwapWarning = false;

    private string apiKeyFilePath;
    private string scriptFolderPath;

    public GeminiTabHandler()
    {
        // 초기화 로직은 Initialize()로 이동합니다.
    }

    public void Initialize(EditorWindow parentWindow)
    {
        if (string.IsNullOrEmpty(scriptFolderPath))
        {
            string scriptPath = AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(parentWindow));
            scriptFolderPath = Path.GetDirectoryName(scriptPath);
            apiKeyFilePath = Path.Combine(scriptFolderPath, "gemini_api_key.txt");
            LoadApiKey();
        }

        if (geminiMessages.Count == 0)
        {
            geminiMessages.Add(new MessageEntry("나: 안녕하세요, 유니티 에디터에서 Gemini 연동 테스트 중입니다.", MessageEntry.MessageType.User));
            geminiMessages.Add(new MessageEntry("Gemini: 반갑습니다! 어떤 것을 도와드릴까요?", MessageEntry.MessageType.AI));
        }
    }

    private void LoadApiKey()
    {
        if (File.Exists(apiKeyFilePath))
        {
            geminiApiKey = File.ReadAllText(apiKeyFilePath).Trim();
            isApiKeyApproved = !string.IsNullOrEmpty(geminiApiKey);
        }
    }

    private async void SaveApiKeyAndValidate()
    {
        if (string.IsNullOrEmpty(geminiApiKey))
        {
            EditorUtility.DisplayDialog("경고", "API 키를 입력해주세요.", "확인");
            return;
        }

        isApprovingApiKey = true;
        EditorWindow.GetWindow<GeminiChatGPTIntegrationEditor>().Repaint();

        string testUrl = "https://generativelanguage.googleapis.com/v1beta/models?key=" + geminiApiKey.Trim();
        using (UnityWebRequest request = UnityWebRequest.Get(testUrl))
        {
            var operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                await Task.Yield();
            }

            if (request.result == UnityWebRequest.Result.Success)
            {
                File.WriteAllText(apiKeyFilePath, geminiApiKey.Trim());
                AssetDatabase.Refresh();
                Debug.Log("Gemini API Key 저장 및 유효성 검사 완료: " + apiKeyFilePath);
                isApiKeyApproved = true;
                EditorUtility.DisplayDialog("성공", "API 키가 성공적으로 승인되었습니다.", "확인");
            }
            else
            {
                isApiKeyApproved = false;
                string errorMessage = request.responseCode == 401 ?
                                      "Gemini: 오류 - 올바르지 않은 API 키입니다. 키를 확인하고 다시 시도해주세요." :
                                      $"Gemini: API 키 유효성 검사 실패 - {request.error} (코드: {request.responseCode})";
                Debug.LogError(errorMessage + "\n" + request.downloadHandler.text);
                EditorUtility.DisplayDialog("오류", errorMessage, "확인");
            }
        }
        
        isApprovingApiKey = false;
        EditorWindow.GetWindow<GeminiChatGPTIntegrationEditor>().Repaint();
    }


    public void OnGUI(float editorWindowWidth, float editorWindowHeight)
    {
        EditorGUILayout.LabelField("✨ Gemini AI와 대화하기", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("🔑 API 설정", EditorStyles.boldLabel);

        if (!isApiKeyApproved)
        {
            EditorGUILayout.HelpBox("Gemini API 키가 입력되지 않았거나 유효하지 않습니다. 아래 버튼을 눌러 발급받거나 입력 후 승인해주세요.", MessageType.Warning);
            if (GUILayout.Button("🚀 Gemini API 키 발급 페이지로 이동", GUILayout.Height(30)))
            {
                Application.OpenURL("https://aistudio.google.com/app/apikey");
            }
        }

        EditorGUILayout.BeginHorizontal();
        GUI.enabled = !isApiKeyApproved && !isApprovingApiKey;
        geminiApiKey = EditorGUILayout.TextField("API Key:", geminiApiKey);
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

        int currentModelIndex = System.Array.IndexOf(availableGeminiModels, geminiAiVersion);
        int newModelIndex = EditorGUILayout.Popup("🤖 현재 구동 중인 AI 모델:", currentModelIndex, availableGeminiModels);
        if (newModelIndex != currentModelIndex)
        {
            geminiAiVersion = availableGeminiModels[newModelIndex];
            showModelUnavailableWarning = false;
        }

        if (showModelUnavailableWarning)
        {
             EditorGUILayout.HelpBox($"⚠️ 현재 선택된 모델 '{geminiAiVersion}'은(는) 사용 불가능합니다. 다른 모델을 시도해보세요.", MessageType.Warning);
        }
        
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(10);

        // '대화 내용' 섹션: 높이를 250으로 고정
        EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Height(250)); 
        EditorGUILayout.LabelField("💬 대화 내용", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);

        geminiScrollPos = EditorGUILayout.BeginScrollView(geminiScrollPos, GUILayout.ExpandHeight(true));
        
        GUIStyle chatStyle = new GUIStyle(EditorStyles.wordWrappedLabel);
        chatStyle.normal.textColor = EditorStyles.label.normal.textColor;
        chatStyle.padding = new RectOffset(5, 5, 5, 5);
        chatStyle.richText = true;

        StringBuilder fullChatContent = new StringBuilder();
        foreach (MessageEntry entry in geminiMessages)
        {
            if (entry.Type == MessageEntry.MessageType.User)
            {
                fullChatContent.AppendLine($"<color=white><b>나:</b> {entry.Content}</color>\n");
            }
            else // AI
            {
                fullChatContent.AppendLine($"<color=#ADD8E6><b>Gemini:</b> {entry.Content}</color>\n"); // 연한 파랑색
            }
        }
        EditorGUILayout.SelectableLabel(fullChatContent.ToString(), chatStyle, GUILayout.ExpandWidth(true));

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(10);

        // '질문하기' 섹션: 높이를 200으로 고정
        EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Height(200)); 
        EditorGUILayout.LabelField("✏️ 질문하기", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);

        if (geminiMessages.Count > 1)
        {
            EditorGUILayout.LabelField($"가장 최근 질문: {geminiMessages[geminiMessages.Count - 2].Content}", EditorStyles.miniLabel);
        }

        GUI.enabled = isApiKeyApproved && !isSendingRequest;

        geminiQuery = EditorGUILayout.TextArea(geminiQuery, GUILayout.ExpandHeight(true)); 

        EditorGUILayout.Space(5);

        if (GUILayout.Button(isSendingRequest ? "⏳ 전송 중..." : "⬆️ 전송", GUILayout.Height(35)))
        {
            SendGeminiQuery(geminiQuery);
            geminiQuery = "";
            showServiceSwapWarning = false;
        }
        GUI.enabled = true;
        
        if (isSendingRequest)
        {
            EditorGUILayout.HelpBox("Gemini 응답을 기다리는 중...", MessageType.Info);
        }
        else if (isApprovingApiKey)
        {
            EditorGUILayout.HelpBox("API 키 유효성 확인 중...", MessageType.Info);
        }
        else if (showServiceSwapWarning)
        {
            EditorGUILayout.HelpBox("⚠️ 답변이 제대로 오지 않았습니다. 다른 구동 서비스(ChatGPT 탭)로 교체해보세요.", MessageType.Warning);
        }
        EditorGUILayout.EndVertical();
    }

    private async void SendGeminiQuery(string query)
    {
        if (string.IsNullOrEmpty(query)) return;
        if (!isApiKeyApproved)
        {
            EditorUtility.DisplayDialog("경고", "API 키를 먼저 승인해주세요.", "확인");
            return;
        }

        isSendingRequest = true;
        showModelUnavailableWarning = false;
        showServiceSwapWarning = false;
        EditorWindow.GetWindow<GeminiChatGPTIntegrationEditor>().Repaint();

        geminiMessages.Add(new MessageEntry(query, MessageEntry.MessageType.User));
        geminiMessages.Add(new MessageEntry("답변 생성 중...", MessageEntry.MessageType.AI));
        
        geminiScrollPos.y = float.MaxValue; 

        string responseText = "오류: 응답을 받지 못했습니다.";

        try
        {
            string url = "https://generativelanguage.googleapis.com/v1beta/models/" + geminiAiVersion + ":generateContent?key=" + geminiApiKey;

            string jsonPayload = "{\"contents\": [{\"parts\": [{\"text\": \"" + EscapeJsonString(query) + "\"}]}]}";
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);

            using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");

                var operation = request.SendWebRequest();
                while (!operation.isDone)
                {
                    await Task.Yield();
                }

                if (request.result == UnityWebRequest.Result.ProtocolError && request.responseCode == 401)
                {
                    responseText = "Gemini: 오류 - 올바르지 않은 API 키입니다. 키를 확인하고 다시 시도해주세요.";
                    Debug.LogError($"Gemini API Key Error: {request.downloadHandler.text}");
                    isApiKeyApproved = false;
                    showServiceSwapWarning = true;
                }
                else if (request.result == UnityWebRequest.Result.ProtocolError && request.responseCode == 404)
                {
                    responseText = $"Gemini: 오류 - 모델 '{geminiAiVersion}'을(를) 찾을 수 없거나 지원되지 않습니다. 다른 모델을 선택해주세요.";
                    Debug.LogError(responseText + "\n" + request.downloadHandler.text);
                    showModelUnavailableWarning = true;
                    showServiceSwapWarning = true;
                }
                else if (request.result == UnityWebRequest.Result.Success)
                {
                    string jsonResponse = request.downloadHandler.text;
                    GeminiResponse geminiResponse = JsonUtility.FromJson<GeminiResponse>(jsonResponse);

                    if (geminiResponse != null && geminiResponse.candidates != null && geminiResponse.candidates.Length > 0 &&
                        geminiResponse.candidates[0].content != null && geminiResponse.candidates[0].content.parts != null &&
                        geminiResponse.candidates[0].content.parts.Length > 0)
                    {
                        responseText = geminiResponse.candidates[0].content.parts[0].text.Trim();
                        showModelUnavailableWarning = false;
                        showServiceSwapWarning = false;

                        string fileName = "AI_Generated_Gemini_Code.cs";
                        string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                        string originalCode = "기존 코드가 있다면 여기에 넣습니다."; 
                        string modifiedCode = responseText;
                        string scriptPath = "Assets/AI_Generated_Scripts/Gemini/";

                        GeminiChatGPTIntegrationEditor editorWindow = EditorWindow.GetWindow<GeminiChatGPTIntegrationEditor>();
                        if (editorWindow != null)
                        {
                            CodeHistoryViewerTabHandler historyHandler = editorWindow.GetCodeHistoryViewerTabHandler();
                            if (historyHandler != null)
                            {
                                historyHandler.RecordCodeChange(fileName, timestamp, originalCode, modifiedCode, scriptPath);
                            }
                            else
                            {
                                Debug.LogError("코드 히스토리 뷰어 핸들러를 찾을 수 없습니다. GeminiChatGPTIntegrationEditor의 OnEnable 메서드를 확인하세요.");
                            }
                        }

                    }
                    else if (geminiResponse != null && geminiResponse.promptFeedback != null && geminiResponse.promptFeedback.blockReason != null)
                    {
                        responseText = $"Gemini: 응답이 차단되었습니다. 이유: {geminiResponse.promptFeedback.blockReason}";
                        Debug.LogWarning(responseText);
                        showServiceSwapWarning = true;
                    }
                    else if (geminiResponse != null && geminiResponse.error != null)
                    {
                        responseText = $"Gemini: 오류 - {geminiResponse.error.message}";
                        Debug.LogError(responseText);
                        showServiceSwapWarning = true;
                    }
                    else
                    {
                        responseText = "오류: Gemini 응답 파싱 실패. 원본: " + jsonResponse;
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
            if (geminiMessages.Count > 0 && geminiMessages[geminiMessages.Count - 1].Content == "답변 생성 중...")
            {
                geminiMessages[geminiMessages.Count - 1].Content = responseText;
            }
            else
            {
                geminiMessages.Add(new MessageEntry(responseText, MessageEntry.MessageType.AI));
            }

            // ⭐ 질문 리스트에 현재 질문과 답변, AI 타입 추가
            GeminiChatGPTIntegrationEditor editorWindow = EditorWindow.GetWindow<GeminiChatGPTIntegrationEditor>();
            if (editorWindow != null)
            {
                QuestionListTabHandler questionListHandler = editorWindow.GetQuestionListTabHandler();
                if (questionListHandler != null)
                {
                    questionListHandler.AddQuestion(query, responseText, AiServiceType.Gemini); 
                }
            }

            isSendingRequest = false;
            geminiScrollPos.y = float.MaxValue;
            EditorWindow.GetWindow<GeminiChatGPTIntegrationEditor>().Repaint();
        }
    }

    private string EscapeJsonString(string text)
    {
        return text.Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
    }

    [System.Serializable]
    private class GeminiResponse
    {
        public Candidate[] candidates;
        public PromptFeedback promptFeedback;
        public ErrorObject error;
    }

    [System.Serializable]
    private class Candidate
    {
        public Content content;
    }

    [System.Serializable]
    private class Content
    {
        public Part[] parts;
    }

    [System.Serializable]
    private class Part
    {
        public string text;
    }

    [System.Serializable]
    private class PromptFeedback
    {
        public string blockReason;
    }

    [System.Serializable]
    private class ErrorObject
    {
        public int code;
        public string message;
        public string status;
    }
}