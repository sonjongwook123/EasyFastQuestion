using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System;

[System.Serializable]
public class StatisticsTabHandler
{
    private GeminiChatGPTIntegrationEditor _parentWindow;
    private List<KeywordEntry> _keywordHistory = new List<KeywordEntry>();
    private string _historyFilePath;
    private string _scriptFolderPath;
    private Vector2 _scrollPos;

    [System.Serializable]
    public class KeywordEntry
    {
        public string Keyword;
        public DateTime Timestamp;

        public KeywordEntry(string keyword, DateTime timestamp)
        {
            Keyword = keyword;
            Timestamp = timestamp;
        }
    }

    [System.Serializable]
    private class KeywordHistoryWrapper
    {
        public KeywordEntry[] Entries;
    }

    public StatisticsTabHandler() { }

    public void Initialize(EditorWindow parentWindow)
    {
        _parentWindow = parentWindow as GeminiChatGPTIntegrationEditor;
        if (string.IsNullOrEmpty(_scriptFolderPath))
        {
            string scriptPath = AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(parentWindow));
            _scriptFolderPath = Path.GetDirectoryName(scriptPath);
            _historyFilePath = Path.Combine(_scriptFolderPath, "keyword_history.json");
            LoadHistory();
        }
    }

    public void RecordKeyword(string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword)) return;

        string trimmedKeyword = keyword.Length > 50 ? keyword.Substring(0, 50) + "..." : keyword;
        trimmedKeyword = trimmedKeyword.Replace("\n", " ").Replace("\r", " ").Trim();

        _keywordHistory.Add(new KeywordEntry(trimmedKeyword, DateTime.Now));
        SaveHistory();
    }

    public void LoadKeywordsAndRefresh()
    {
        LoadHistory();
        _parentWindow?.Repaint();
    }

    private void LoadHistory()
    {
        if (File.Exists(_historyFilePath))
        {
            string json = File.ReadAllText(_historyFilePath);
            try
            {
                KeywordHistoryWrapper wrapper = JsonUtility.FromJson<KeywordHistoryWrapper>(json);
                if (wrapper != null && wrapper.Entries != null)
                {
                    _keywordHistory = new List<KeywordEntry>(wrapper.Entries);
                }
                else
                {
                    _keywordHistory = new List<KeywordEntry>();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to load keyword history: {e.Message}");
                _keywordHistory = new List<KeywordEntry>();
            }
        }
        else
        {
            _keywordHistory = new List<KeywordEntry>();
        }
    }

    private void SaveHistory()
    {
        if (_keywordHistory == null) _keywordHistory = new List<KeywordEntry>();
        KeywordHistoryWrapper wrapper = new KeywordHistoryWrapper { Entries = _keywordHistory.ToArray() };
        string json = JsonUtility.ToJson(wrapper, true);
        File.WriteAllText(_historyFilePath, json);
    }

    public void OnGUI(float editorWindowWidth, float editorWindowHeight)
    {
        EditorGUILayout.LabelField("📊 통계 분석", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.ExpandHeight(true));

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("📈 가장 많이 검색한 키워드 (Top 20)", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);

        DateTime sevenDaysAgo = DateTime.Now.AddDays(-7);
        var recentKeywords = _keywordHistory
            .Where(entry => entry.Timestamp >= sevenDaysAgo)
            .ToList();

        // 조건 변경: recentKeywords가 비어있지 않으면 Top Keywords를 표시합니다.
        if (recentKeywords.Any())
        {
            var topKeywords = recentKeywords
                .GroupBy(entry => entry.Keyword)
                .Select(group => new { Keyword = group.Key, Count = group.Count() })
                .OrderByDescending(x => x.Count)
                .Take(20)
                .ToList();

            if (topKeywords.Any()) // topKeywords가 실제로 존재하는지 다시 확인 (Take(20)으로 인해 없을 수도 있음)
            {
                for (int i = 0; i < topKeywords.Count; i += 5)
                {
                    EditorGUILayout.BeginHorizontal();
                    for (int j = 0; j < 5 && (i + j) < topKeywords.Count; j++)
                    {
                        var keywordData = topKeywords[i + j];
                        GUIStyle rankStyle = new GUIStyle(EditorStyles.label);
                        rankStyle.alignment = TextAnchor.MiddleLeft;
                        rankStyle.normal.textColor = (i+j < 3) ? Color.yellow : EditorStyles.label.normal.textColor;
                        rankStyle.fontStyle = FontStyle.Bold;

                        EditorGUILayout.LabelField($"{(i + j + 1)}. {keywordData.Keyword} ({keywordData.Count}회)", rankStyle);
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }
            else
            {
                EditorGUILayout.HelpBox("최근 일주일간 검색된 키워드가 없습니다.", MessageType.Info);
            }
        }
        else
        {
            EditorGUILayout.HelpBox("아직 검색 키워드 내역이 없습니다. 질문을 입력하여 통계를 쌓아보세요!", MessageType.Info);
        }
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(20);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("📊 주 단위 일별 검색 키워드 수", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);

        var dailyKeywordCounts = _keywordHistory
            .Where(entry => entry.Timestamp >= DateTime.Now.AddDays(-7))
            .GroupBy(entry => entry.Timestamp.Date)
            .OrderBy(group => group.Key)
            .ToDictionary(group => group.Key, group => group.Count());

        // 조건 변경: dailyKeywordCounts가 비어있지 않으면 그래프를 표시합니다.
        if (dailyKeywordCounts.Any())
        {
            int maxCount = dailyKeywordCounts.Values.Any() ? dailyKeywordCounts.Values.Max() : 1;
            if (maxCount == 0) maxCount = 1;

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            foreach (var dailyData in dailyKeywordCounts)
            {
                EditorGUILayout.BeginVertical(GUILayout.Width(50));
                float barHeight = (float)dailyData.Value / maxCount * 100f;
                Rect barRect = GUILayoutUtility.GetRect(20, barHeight);
                EditorGUI.DrawRect(barRect, new Color(0.2f, 0.7f, 0.9f, 1f));
                EditorGUILayout.LabelField(dailyData.Value.ToString(), EditorStyles.miniLabel, GUILayout.ExpandWidth(true));
                EditorGUILayout.LabelField(dailyData.Key.ToString("MM/dd"), EditorStyles.miniLabel, GUILayout.ExpandWidth(true));
                EditorGUILayout.EndVertical();
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }
        else
        {
            EditorGUILayout.HelpBox("최근 일주일간 검색 키워드 활동이 없습니다.", MessageType.Info);
        }
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(20);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("🔍 검색 키워드 분석", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);

        EditorGUILayout.HelpBox("가장 많이 검색한 키워드들을 기반으로 AI에게 취약한 부분에 대한 분석을 요청합니다.", MessageType.Info);

        EditorGUILayout.BeginHorizontal();
        // 분석 버튼은 키워드가 하나라도 있으면 활성화됩니다.
        GUI.enabled = _keywordHistory.Any(); 
        if (GUILayout.Button("Gemini로 분석", GUILayout.Height(35)))
        {
            AnalyzeKeywordsWithAI(AiServiceType.Gemini);
        }
        if (GUILayout.Button("ChatGPT로 분석", GUILayout.Height(35)))
        {
            AnalyzeKeywordsWithAI(AiServiceType.ChatGPT);
        }
        GUI.enabled = true; // GUI 활성화 상태 복원
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(10);

        if (GUILayout.Button("통계 데이터 지우기", GUILayout.Height(30)))
        {
            if (EditorUtility.DisplayDialog("데이터 지우기", "정말로 모든 통계 데이터를 지우시겠습니까? (검색 키워드 내역)", "예", "아니오"))
            {
                _keywordHistory.Clear();
                SaveHistory();
                _parentWindow?.Repaint();
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private async void AnalyzeKeywordsWithAI(AiServiceType aiType)
    {
        DateTime sevenDaysAgo = DateTime.Now.AddDays(-7);
        var top5Keywords = _keywordHistory
            .Where(entry => entry.Timestamp >= sevenDaysAgo)
            .GroupBy(entry => entry.Keyword)
            .Select(group => new { Keyword = group.Key, Count = group.Count() })
            .OrderByDescending(x => x.Count)
            .Take(5)
            .ToList();

        // 키워드 데이터가 없을 때도 분석 요청 버튼은 표시되지만, 실제 요청 시 경고를 띄웁니다.
        if (!top5Keywords.Any())
        {
            EditorUtility.DisplayDialog("경고", "분석할 키워드 데이터가 충분하지 않습니다. 먼저 질문을 하여 키워드를 쌓아주세요.", "확인");
            return;
        }

        string analysisQuery = "가장 많이 검색된 키워드들은 다음과 같습니다: \n" +
                                string.Join("\n", top5Keywords.Select(k => $"- '{k.Keyword}' ({k.Count}회)")) +
                                "\n\n이 키워드들을 기반으로 사용자(개발자)가 어떤 부분에 취약하거나 도움이 필요해 보이는지 분석하고, 이에 대한 개선 방안이나 학습 자료 등을 제안해 주세요. 답변은 간결하고 실용적인 조언을 중심으로 해주세요.";

        GeminiChatGPTIntegrationEditor editorWindow = _parentWindow;
        if (editorWindow == null) return;

        bool success = false;

        if (aiType == AiServiceType.Gemini)
        {
            GeminiTabHandler geminiHandler = editorWindow.GetGeminiTabHandler();
            if (geminiHandler != null)
            {
                if (!geminiHandler.IsApiKeyApproved())
                {
                    EditorUtility.DisplayDialog("경고", "Gemini API 키가 승인되지 않아 분석을 요청할 수 없습니다. Gemini 탭에서 API 키를 입력하고 승인해주세요.", "확인");
                    return;
                }
                if (geminiHandler.IsSendingRequest())
                {
                    EditorUtility.DisplayDialog("경고", "Gemini가 이미 다른 요청을 처리 중입니다. 잠시 후 다시 시도해주세요.", "확인");
                    return;
                }
                if (geminiHandler.IsModelUnavailable())
                {
                    EditorUtility.DisplayDialog("경고", "현재 Gemini 모델이 사용 불가능합니다. Gemini 탭에서 다른 모델을 선택해주세요.", "확인");
                    return;
                }
                geminiHandler.SendGeminiQuery(analysisQuery, true);
                success = true;
                EditorUtility.DisplayDialog("분석 요청", "Gemini에게 키워드 분석을 요청했습니다. Gemini 탭에서 답변을 확인해주세요.", "확인");
            }
        }
        else if (aiType == AiServiceType.ChatGPT)
        {
            ChatGPTTabHandler chatGPTHandler = editorWindow.GetChatGPTTabHandler();
            if (chatGPTHandler != null)
            {
                if (!chatGPTHandler.IsApiKeyApproved())
                {
                    EditorUtility.DisplayDialog("경고", "ChatGPT API 키가 승인되지 않아 분석을 요청할 수 없습니다. ChatGPT 탭에서 API 키를 입력하고 승인해주세요.", "확인");
                    return;
                }
                if (chatGPTHandler.IsSendingRequest())
                {
                    EditorUtility.DisplayDialog("경고", "ChatGPT가 이미 다른 요청을 처리 중입니다. 잠시 후 다시 시도해주세요.", "확인");
                    return;
                }
                chatGPTHandler.SendChatGPTQuery(analysisQuery, true);
                success = true;
                EditorUtility.DisplayDialog("분석 요청", "ChatGPT에게 키워드 분석을 요청했습니다. ChatGPT 탭에서 답변을 확인해주세요.", "확인");
            }
        }

        if (success)
        {
            _parentWindow.Repaint();
        }
    }
}