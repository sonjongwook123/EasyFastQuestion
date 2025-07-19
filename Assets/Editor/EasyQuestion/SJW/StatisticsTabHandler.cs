using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Threading.Tasks;
using System.Text; 

[System.Serializable]
public class StatisticsTabHandler
{
    private GeminiChatGPTIntegrationEditor _parentWindow;
    private Vector2 _scrollPos;
    private Vector2 _analysisScrollPos; 

    private List<KeywordLogEntry> _keywordLogs = new List<KeywordLogEntry>();
    private List<AnalysisResultEntry> _analysisHistory = new List<AnalysisResultEntry>(); 
    private string _statisticsFilePath;
    private string _analysisHistoryFilePath; 
    private string _scriptFolderPath;

    private bool _isAIAnalysisInProgress = false;

    private const int KeywordDaysPerPage = 4;
    private int _keywordCurrentPage = 0; 

    private const int AnalysisEntriesPerPage = 3;
    private int _analysisCurrentPage = 0; 

    private int _selectedAnalysisCategoryTab = 0; 
    private string[] _analysisCategoryTabNames = { "전체", "중요", "Gemini", "ChatGPT" };


    [System.Serializable]
    private class KeywordStatisticsWrapper
    {
        public KeywordLogEntry[] KeywordLogs;
    }

    [System.Serializable]
    public class KeywordLogEntry
    {
        public string Keyword;
        public DateTime Timestamp;

        public KeywordLogEntry(string keyword, DateTime timestamp)
        {
            Keyword = keyword;
            Timestamp = timestamp;
        }
    }

    [System.Serializable]
    public class AnalysisResultEntry
    {
        public AiServiceType ServiceType;
        public string Prompt;
        public string Response;
        public DateTime Timestamp;
        public bool IsImportant;

        public AnalysisResultEntry(AiServiceType serviceType, string prompt, string response, DateTime timestamp)
        {
            ServiceType = serviceType;
            Prompt = prompt;
            Response = response;
            Timestamp = timestamp;
            IsImportant = false; 
        }
    }

    [System.Serializable]
    private class AnalysisHistoryWrapper
    {
        public AnalysisResultEntry[] AnalysisLogs;
    }

    public StatisticsTabHandler() { }

    public void Initialize(EditorWindow parentWindow)
    {
        _parentWindow = parentWindow as GeminiChatGPTIntegrationEditor;
        if (string.IsNullOrEmpty(_scriptFolderPath))
        {
            string[] guids = AssetDatabase.FindAssets("t:Script " + typeof(StatisticsTabHandler).Name);
            if (guids.Length > 0)
            {
                string scriptPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                _scriptFolderPath = Path.GetDirectoryName(scriptPath);
                _statisticsFilePath = Path.Combine(_scriptFolderPath, "keyword_statistics.json");
                _analysisHistoryFilePath = Path.Combine(_scriptFolderPath, "analysis_history.json");
            }
            else
            {
                Debug.LogError("StatisticsTabHandler.cs 파일을 찾을 수 없습니다. 경로 설정을 수동으로 확인해주세요.");
                _scriptFolderPath = Application.dataPath + "/Editor";
                _statisticsFilePath = Path.Combine(_scriptFolderPath, "keyword_statistics.json");
                _analysisHistoryFilePath = Path.Combine(_scriptFolderPath, "analysis_history.json");
            }
            LoadStatistics(); 
            LoadAnalysisHistory();
        }
    }

    public bool IsAIAnalysisInProgress()
    {
        return _isAIAnalysisInProgress;
    }

    public void SetAIAnalysisInProgress(bool inProgress)
    {
        _isAIAnalysisInProgress = inProgress;
        _parentWindow?.Repaint();
    }

    public void OnGUI(float editorWindowWidth, float editorWindowHeight)
    {
        EditorGUILayout.LabelField("📊 통계 분석", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        GenerateKeywordStatisticsFromQuestions();

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("기간별 키워드 사용 빈도", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);

        List<DateTime> uniqueDates = _keywordLogs.Select(log => log.Timestamp.Date).Distinct().OrderByDescending(d => d).ToList();
        int totalKeywordPages = Mathf.CeilToInt((float)uniqueDates.Count / KeywordDaysPerPage);

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUI.enabled = (_keywordCurrentPage < totalKeywordPages - 1);
        if (GUILayout.Button("이전 기간 ◀", GUILayout.Width(100)))
        {
            _keywordCurrentPage++;
            _parentWindow?.Repaint();
        }
        GUI.enabled = (_keywordCurrentPage > 0);
        if (GUILayout.Button("다음 기간 ▶", GUILayout.Width(100)))
        {
            _keywordCurrentPage--;
            _parentWindow?.Repaint();
        }
        GUI.enabled = true;
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(5);

        // Keyword scroll view
        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.ExpandWidth(true), GUILayout.Height(200));

        if (!uniqueDates.Any())
        {
            EditorGUILayout.HelpBox("아직 기록된 키워드가 없습니다. 질문을 시작하여 통계를 쌓아보세요!", MessageType.Info);
        }
        else
        {
            List<DateTime> currentDates = uniqueDates.Skip(_keywordCurrentPage * KeywordDaysPerPage).Take(KeywordDaysPerPage).ToList();
            foreach (DateTime date in currentDates)
            {
                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField($"--- {date.ToString("yyyy년 MM월 dd일", CultureInfo.CurrentCulture)} ---", EditorStyles.boldLabel);
                EditorGUILayout.Space(5);
                var keywordsForDate = _keywordLogs
                    .Where(log => log.Timestamp.Date == date)
                    .GroupBy(log => log.Keyword)
                    .Select(g => new { Keyword = g.Key, Count = g.Count() })
                    .OrderByDescending(x => x.Count)
                    .ToList();

                if (!keywordsForDate.Any())
                {
                    EditorGUILayout.LabelField("해당 날짜에 키워드가 없습니다.");
                }
                else
                {
                    foreach (var item in keywordsForDate)
                    {
                        EditorGUILayout.LabelField($"- {item.Keyword}: {item.Count}회");
                    }
                }
            }
        }
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(10);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("AI로 질문 내역 분석", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("모든 질문 내역을 기반으로 AI가 분석을 수행합니다.", MessageType.Info);

        GUI.enabled = !_isAIAnalysisInProgress;

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Gemini 분석 시작", GUILayout.Height(40)))
        {
            RunAIAnalysis(AiServiceType.Gemini);
        }
        if (GUILayout.Button("ChatGPT 분석 시작", GUILayout.Height(40)))
        {
            RunAIAnalysis(AiServiceType.ChatGPT);
        }
        EditorGUILayout.EndHorizontal();

        GUI.enabled = true;

        if (_isAIAnalysisInProgress)
        {
            EditorGUILayout.HelpBox("AI가 질문 내역을 분석 중입니다. 잠시 기다려주세요...", MessageType.Info);
        }
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(10);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("AI 분석 히스토리", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);

        int newSelectedAnalysisCategoryTab = GUILayout.Toolbar(_selectedAnalysisCategoryTab, _analysisCategoryTabNames);
        if (newSelectedAnalysisCategoryTab != _selectedAnalysisCategoryTab)
        {
            _selectedAnalysisCategoryTab = newSelectedAnalysisCategoryTab;
            _analysisCurrentPage = 0; 
            _parentWindow?.Repaint();
        }
        EditorGUILayout.Space(5);


        List<AnalysisResultEntry> filteredAnalysisHistory = FilterAnalysisHistory();
        int totalAnalysisPages = Mathf.CeilToInt((float)filteredAnalysisHistory.Count / AnalysisEntriesPerPage);

        // Pagination controls for analysis history
        if (totalAnalysisPages > 1) // Only show controls if more than one page
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUI.enabled = (_analysisCurrentPage < totalAnalysisPages - 1);
            if (GUILayout.Button("이전 기록 ◀", GUILayout.Width(100)))
            {
                _analysisCurrentPage++;
                _parentWindow?.Repaint();
            }
            GUI.enabled = (_analysisCurrentPage > 0);
            if (GUILayout.Button("다음 기록 ▶", GUILayout.Width(100)))
            {
                _analysisCurrentPage--;
                _parentWindow?.Repaint();
            }
            GUI.enabled = true;
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(5);
        }


        _analysisScrollPos = EditorGUILayout.BeginScrollView(_analysisScrollPos, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        if (!filteredAnalysisHistory.Any())
        {
            EditorGUILayout.HelpBox("아직 AI 분석 기록이 없습니다. 위에 있는 버튼으로 AI 분석을 시작해보세요!", MessageType.Info);
        }
        else
        {
            List<AnalysisResultEntry> currentAnalysisEntries = filteredAnalysisHistory
                .Skip(_analysisCurrentPage * AnalysisEntriesPerPage)
                .Take(AnalysisEntriesPerPage)
                .ToList();

            foreach (var entry in currentAnalysisEntries)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"<b>[{entry.ServiceType} 분석]</b> - {entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss")}", GetRichTextStyleBold());
                GUILayout.FlexibleSpace();
                bool newIsImportant = GUILayout.Toggle(entry.IsImportant, new GUIContent(entry.IsImportant ? "★ 중요" : "☆ 중요", "이 분석을 중요 표시합니다."), GUILayout.Width(60));
                if (newIsImportant != entry.IsImportant)
                {
                    entry.IsImportant = newIsImportant;
                    SaveAnalysisHistory();
                    _parentWindow?.Repaint();
                }
                EditorGUILayout.EndHorizontal();

                StringBuilder combinedText = new StringBuilder();
                combinedText.AppendLine($"<color=#3366FF><b>[요청]</b></color> {entry.Prompt}");
                combinedText.AppendLine();
                combinedText.AppendLine($"<color=#008000><b>[응답]</b></color> {entry.Response}");

                GUIStyle chatStyle = new GUIStyle(EditorStyles.textArea);
                chatStyle.richText = true;
                chatStyle.wordWrap = true;
                float textHeight = chatStyle.CalcHeight(new GUIContent(combinedText.ToString()), editorWindowWidth - 40); 
                EditorGUILayout.SelectableLabel(combinedText.ToString(), chatStyle, GUILayout.MinHeight(50), GUILayout.Height(textHeight));
                
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(10);
            }
        }
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private List<AnalysisResultEntry> FilterAnalysisHistory()
    {
        IEnumerable<AnalysisResultEntry> query = _analysisHistory.OrderByDescending(e => e.Timestamp).AsEnumerable();

        switch (_selectedAnalysisCategoryTab)
        {
            case 1: 
                query = query.Where(q => q.IsImportant);
                break;
            case 2: 
                query = query.Where(q => q.ServiceType == AiServiceType.Gemini);
                break;
            case 3: 
                query = query.Where(q => q.ServiceType == AiServiceType.ChatGPT);
                break;
            default: // 전체 (case 0)
                break;
        }
        return query.ToList();
    }


    private void GenerateKeywordStatisticsFromQuestions()
    {
        _keywordLogs.Clear();
        QuestionListTabHandler questionListHandler = _parentWindow?.GetQuestionListTabHandler();
        if (questionListHandler == null)
        {
            Debug.LogError("QuestionListTabHandler를 찾을 수 없습니다.");
            return;
        }

        foreach (var questionEntry in questionListHandler._questions)
        {
            string questionText = questionEntry.Question;
            string[] words = Regex.Split(questionText, @"[\s.,;!?'""“”‘’—–_\(\)\[\]\{\}-]+", RegexOptions.Compiled)
                                  .Where(s => !string.IsNullOrWhiteSpace(s) && s.Length > 1) 
                                  .ToArray();

            foreach (string word in words)
            {
                string normalizedWord = word.ToLower(CultureInfo.InvariantCulture).Trim();
                if (string.IsNullOrWhiteSpace(normalizedWord)) continue;
                _keywordLogs.Add(new KeywordLogEntry(normalizedWord, questionEntry.Timestamp)); 
            }
        }
    }

    private async void RunAIAnalysis(AiServiceType serviceType)
    {
        if (_isAIAnalysisInProgress) return;

        SetAIAnalysisInProgress(true);
        string analysisPrompt = GenerateAnalysisPrompt();
        string aiResponse = "";
        bool success = false;

        try
        {
            if (serviceType == AiServiceType.Gemini)
            {
                GeminiTabHandler geminiHandler = _parentWindow?.GetGeminiTabHandler();
                if (geminiHandler != null && !string.IsNullOrEmpty(geminiHandler.GetApiKey()))
                {
                    aiResponse = await geminiHandler.SendGeminiAnalysisRequest(analysisPrompt);
                    success = true;
                }
                else
                {
                    aiResponse = "Gemini API 키가 설정되지 않았거나 핸들러를 찾을 수 없습니다.";
                }
            }
            else if (serviceType == AiServiceType.ChatGPT)
            {
                ChatGPTTabHandler chatGPTHandler = _parentWindow?.GetChatGPTTabHandler();
                if (chatGPTHandler != null && !string.IsNullOrEmpty(chatGPTHandler.GetApiKey()))
                {
                    aiResponse = await chatGPTHandler.SendChatGPTAnalysisRequest(analysisPrompt);
                    success = true;
                }
                else
                {
                    aiResponse = "ChatGPT API 키가 설정되지 않았거나 핸들러를 찾을 수 없습니다.";
                }
            }
        }
        catch (Exception e)
        {
            aiResponse = $"AI 분석 중 오류 발생: {e.Message}";
            Debug.LogError($"AI Analysis Error: {e.Message}");
        }
        finally
        {
            // Always log the analysis attempt, even if it failed, with the error message
            _analysisHistory.Insert(0, new AnalysisResultEntry(serviceType, analysisPrompt, aiResponse, DateTime.Now));
            SaveAnalysisHistory();
            SetAIAnalysisInProgress(false);
        }
    }

    private string GenerateAnalysisPrompt()
    {
        QuestionListTabHandler questionListHandler = _parentWindow?.GetQuestionListTabHandler();
        if (questionListHandler == null || !questionListHandler._questions.Any())
        {
            return "분석할 질문 내역이 없습니다.";
        }

        StringBuilder promptBuilder = new StringBuilder();
        promptBuilder.AppendLine("다음은 사용자가 AI에게 질문했던 모든 내역입니다. 이 질문들을 종합적으로 분석하여 사용자의 주요 관심사, 자주 묻는 주제, 그리고 질문 패턴에 대한 상세한 보고서를 작성해 주세요. 각 질문의 날짜와 시간을 함께 고려하여 시간 흐름에 따른 변화도 파악해 주십시오.");
        promptBuilder.AppendLine("--- 질문 내역 시작 ---");
        foreach (var entry in questionListHandler._questions.OrderBy(q => q.Timestamp))
        {
            promptBuilder.AppendLine($"- [날짜: {entry.Timestamp:yyyy-MM-dd HH:mm:ss}, 유형: {entry.ServiceType}]: {entry.Question}");
        }
        promptBuilder.AppendLine("--- 질문 내역 끝 ---");
        promptBuilder.AppendLine("분석 보고서는 다음 내용을 포함해야 합니다:");
        promptBuilder.AppendLine("1. 전체적인 질문 주제 및 경향");
        promptBuilder.AppendLine("2. 특정 기간(예: 주간, 월간) 동안의 질문량 변화");
        promptBuilder.AppendLine("3. 가장 자주 등장하는 키워드와 그 키워드의 의미");
        promptBuilder.AppendLine("4. Gemini와 ChatGPT 각각의 사용 빈도 및 특징적인 질문 유형 비교");
        promptBuilder.AppendLine("5. 사용자가 중요하게 표시한 질문들의 특징 (만약 있다면)");
        promptBuilder.AppendLine("6. 전반적인 사용자의 관심사 변화 추이");
        promptBuilder.AppendLine("7. 기타 발견된 특이사항 또는 제안.");
        promptBuilder.AppendLine("8. 취약점 분석 밑 앞으로의 방향 제안.");
        promptBuilder.AppendLine("보고서는 명확하고 간결하게 작성해 주십시오.");

        return promptBuilder.ToString();
    }


    private void LoadStatistics()
    {
        if (File.Exists(_statisticsFilePath))
        {
            string json = File.ReadAllText(_statisticsFilePath);
            try
            {
                KeywordStatisticsWrapper wrapper = JsonUtility.FromJson<KeywordStatisticsWrapper>(json);
                if (wrapper != null && wrapper.KeywordLogs != null)
                {
                    _keywordLogs = new List<KeywordLogEntry>(wrapper.KeywordLogs);
                }
                else
                {
                    _keywordLogs = new List<KeywordLogEntry>();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to load keyword statistics: {e.Message}");
                _keywordLogs = new List<KeywordLogEntry>();
            }
        }
        else
        {
            _keywordLogs = new List<KeywordLogEntry>();
        }
    }

    private void SaveStatistics()
    {
        if (_keywordLogs == null) _keywordLogs = new List<KeywordLogEntry>();
        KeywordStatisticsWrapper wrapper = new KeywordStatisticsWrapper
        {
            KeywordLogs = _keywordLogs.ToArray()
        };
        string json = JsonUtility.ToJson(wrapper, true);
        File.WriteAllText(_statisticsFilePath, json);
        AssetDatabase.Refresh();
    }

    private void LoadAnalysisHistory()
    {
        if (File.Exists(_analysisHistoryFilePath))
        {
            string json = File.ReadAllText(_analysisHistoryFilePath);
            try
            {
                AnalysisHistoryWrapper wrapper = JsonUtility.FromJson<AnalysisHistoryWrapper>(json);
                if (wrapper != null && wrapper.AnalysisLogs != null)
                {
                    _analysisHistory = new List<AnalysisResultEntry>(wrapper.AnalysisLogs);
                    foreach (var entry in _analysisHistory)
                    {
                    }
                }
                else
                {
                    _analysisHistory = new List<AnalysisResultEntry>();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to load analysis history: {e.Message}");
                _analysisHistory = new List<AnalysisResultEntry>();
            }
        }
        else
        {
            _analysisHistory = new List<AnalysisResultEntry>();
        }
    }

    public void SaveAnalysisHistory()
    {
        if (_analysisHistory == null) _analysisHistory = new List<AnalysisResultEntry>();
        AnalysisHistoryWrapper wrapper = new AnalysisHistoryWrapper
        {
            AnalysisLogs = _analysisHistory.ToArray()
        };
        string json = JsonUtility.ToJson(wrapper, true);
        File.WriteAllText(_analysisHistoryFilePath, json);
        AssetDatabase.Refresh();
    }

    private GUIStyle GetRichTextStyleBold()
    {
        GUIStyle style = new GUIStyle(EditorStyles.label);
        style.richText = true;
        style.fontStyle = FontStyle.Bold;
        style.wordWrap = true;
        return style;
    }
}