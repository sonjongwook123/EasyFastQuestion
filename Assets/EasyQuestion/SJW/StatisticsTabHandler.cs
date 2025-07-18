using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Threading.Tasks;

[System.Serializable]
public class StatisticsTabHandler
{
    private GeminiChatGPTIntegrationEditor _parentWindow;
    private Vector2 _scrollPos;

    private List<KeywordLogEntry> _keywordLogs = new List<KeywordLogEntry>();
    private string _statisticsFilePath;
    private string _scriptFolderPath;

    private bool _isAIAnalysisInProgress = false; // 추가: AI 분석 진행 상태 플래그

    private const int DaysPerPage = 4;
    private int _currentPage = 0;

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
            }
            else
            {
                Debug.LogError("StatisticsTabHandler.cs 파일을 찾을 수 없습니다. 경로 설정을 수동으로 확인해주세요.");
                _scriptFolderPath = Application.dataPath + "/Editor"; // fallback
                _statisticsFilePath = Path.Combine(_scriptFolderPath, "keyword_statistics.json");
            }
            LoadStatistics();
        }
    }

    public void RecordKeyword(string keyword)
    {
        // 불필요한 문자를 제거하고 단어만 추출
        string[] words = Regex.Split(keyword, @"[\s.,;!?-]+", RegexOptions.Compiled)
                              .Where(s => !string.IsNullOrWhiteSpace(s))
                              .ToArray();

        foreach (string word in words)
        {
            string normalizedWord = word.ToLower(CultureInfo.InvariantCulture).Trim();
            // 한글 음절 분리 방지 및 영어 단어 최소 길이 설정 (필요시)
            // 여기서는 단순히 공백 제거 후 기록
            if (string.IsNullOrWhiteSpace(normalizedWord)) continue;
            _keywordLogs.Add(new KeywordLogEntry(normalizedWord, DateTime.Now));
        }
        SaveStatistics();
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

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("기간별 키워드 사용 빈도", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);

        List<DateTime> uniqueDates = _keywordLogs.Select(log => log.Timestamp.Date).Distinct().OrderByDescending(d => d).ToList();
        int totalPages = Mathf.CeilToInt((float)uniqueDates.Count / DaysPerPage);

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUI.enabled = (_currentPage < totalPages - 1);
        if (GUILayout.Button("이전 기간 ◀", GUILayout.Width(100)))
        {
            _currentPage++;
            _parentWindow?.Repaint();
        }
        GUI.enabled = (_currentPage > 0);
        if (GUILayout.Button("다음 기간 ▶", GUILayout.Width(100)))
        {
            _currentPage--;
            _parentWindow?.Repaint();
        }
        GUI.enabled = true;
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(5);

        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

        if (!uniqueDates.Any())
        {
            EditorGUILayout.HelpBox("아직 기록된 키워드가 없습니다. 질문을 시작하여 통계를 쌓아보세요!", MessageType.Info);
        }
        else
        {
            List<DateTime> currentDates = uniqueDates.Skip(_currentPage * DaysPerPage).Take(DaysPerPage).ToList();
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

                foreach (var item in keywordsForDate)
                {
                    EditorGUILayout.LabelField($"- {item.Keyword}: {item.Count}회");
                }
            }
        }
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(10);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("AI로 분석하기", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("모든 질문 내역에서 키워드를 추출하여 AI가 내용을 분석하고 답변합니다.", MessageType.Info);
        GUI.enabled = !_isAIAnalysisInProgress; // 분석 중일 때는 버튼 비활성화
        if (GUILayout.Button("AI 분석 시작", GUILayout.Height(40)))
        {
            RunAIAnalysis();
        }
        GUI.enabled = true; // GUI 상태를 다시 활성화
        if (_isAIAnalysisInProgress)
        {
            EditorGUILayout.HelpBox("Gemini AI가 질문 내역을 분석 중입니다. 잠시 기다려주세요...", MessageType.Info);
        }
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(10);
    }

    // ⭐ 수정: 모든 질문 내역에서 키워드를 추출하여 AI 분석 요청
    private async void RunAIAnalysis()
    {
        if (_isAIAnalysisInProgress) return; // 이미 분석 중이면 중복 실행 방지

        SetAIAnalysisInProgress(true); // 분석 시작 상태로 설정

        QuestionListTabHandler questionListHandler = _parentWindow.GetQuestionListTabHandler();
        List<QuestionListTabHandler.QuestionEntry> allQuestions = questionListHandler._questions;

        if (!allQuestions.Any())
        {
            EditorUtility.DisplayDialog("정보", "분석할 질문 내역이 없습니다.", "확인");
            SetAIAnalysisInProgress(false);
            return;
        }

        // 모든 질문과 답변 텍스트 수집
        List<string> allTexts = new List<string>();
        foreach (var entry in allQuestions)
        {
            allTexts.Add(entry.Question);
            allTexts.Add(entry.Answer);
            foreach (var memo in entry.Memos)
            {
                allTexts.Add(memo.Content);
            }
        }

        // 텍스트에서 한글 단어 및 영어 단어 추출 및 빈도 계산
        Dictionary<string, int> keywordFrequencies = new Dictionary<string, int>();
        Regex wordRegex = new Regex(@"[가-힣a-zA-Z]+"); // 한글과 영어 알파벳만 매치

        foreach (string text in allTexts)
        {
            MatchCollection matches = wordRegex.Matches(text);
            foreach (Match match in matches)
            {
                string word = match.Value.ToLower(CultureInfo.InvariantCulture);
                if (word.Length < 2) continue; // 한 글자 단어는 무시 (필요시 조정)

                // 불용어 필터링 (간단한 예시, 필요시 더 많은 불용어 추가)
                string[] stopWords = { "은", "는", "이", "가", "을", "를", "와", "과", "에", "의", "하고", "그리고", "하지만", "있다", "없다", "합니다", "이다", "것", "수", "등", "있습니다", "있어요", "합니다", "주세요", "입니다", "합니다", "하여", "에서", "으로", "어떤", "어떻게", "무엇", "언제", "어디", "누가", "왜", "어떻게", "좀" };
                if (stopWords.Contains(word)) continue;

                if (keywordFrequencies.ContainsKey(word))
                {
                    keywordFrequencies[word]++;
                }
                else
                {
                    keywordFrequencies.Add(word, 1);
                }
            }
        }

        // 빈도수 높은 순으로 상위 N개 키워드 추출
        var topKeywords = keywordFrequencies.OrderByDescending(pair => pair.Value)
                                            .Take(50) // 상위 50개 키워드
                                            .ToList();

        if (!topKeywords.Any())
        {
            EditorUtility.DisplayDialog("정보", "분석할 유의미한 키워드를 찾을 수 없습니다. 질문 내역을 확인해주세요.", "확인");
            SetAIAnalysisInProgress(false);
            return;
        }

        // AI에 보낼 프롬프트 생성
        System.Text.StringBuilder analysisPromptBuilder = new System.Text.StringBuilder();
        analysisPromptBuilder.AppendLine("다음은 사용자의 질문 내역에서 추출된 주요 키워드와 해당 빈도수 목록입니다. 이 데이터를 기반으로 사용자의 관심사, 가장 자주 언급되는 주제, 그리고 전반적인 질문 경향에 대해 종합적으로 분석하여 답변해주세요.");
        analysisPromptBuilder.AppendLine("\n--- 키워드 목록 ---");
        foreach (var keyword in topKeywords)
        {
            analysisPromptBuilder.AppendLine($"- {keyword.Key}: {keyword.Value}회");
        }
        analysisPromptBuilder.AppendLine("\n--- 분석 요청 ---");
        analysisPromptBuilder.AppendLine("위 키워드 데이터를 바탕으로 사용자의 질문 경향을 심층적으로 분석하고, 다음 질문에 도움이 될 만한 통찰력을 제공해주세요.");
        analysisPromptBuilder.AppendLine("예시: 사용자는 주로 어떤 주제에 관심이 많으며, 어떤 종류의 질문을 자주 하는가? 특정 키워드의 급증은 어떤 의미를 가지는가? 전반적인 질문 패턴에서 발견할 수 있는 유의미한 특징은 무엇인가?");

        string analysisPrompt = analysisPromptBuilder.ToString();

        // GeminiTabHandler를 통해 Gemini AI로 요청 전송
        GeminiTabHandler geminiHandler = _parentWindow.GetGeminiTabHandler();
        if (geminiHandler != null)
        {
            // GeminiTabHandler의 _messages 리스트가 오염되지 않도록 임시로 IsAIAnalysisInProgress 플래그 사용
            // SendGeminiQuery 내부에서 이 플래그를 확인하여 메시지 기록 여부 결정
            await Task.Run(() => geminiHandler.SendGeminiQuery(analysisPrompt));
        }
        else
        {
            Debug.LogError("GeminiTabHandler를 찾을 수 없습니다.");
            EditorUtility.DisplayDialog("오류", "Gemini AI 분석 기능을 사용할 수 없습니다. GeminiTabHandler를 초기화하지 못했습니다.", "확인");
            SetAIAnalysisInProgress(false); // 오류 발생 시 분석 상태 해제
        }
        
        // 분석 완료는 SendGeminiQuery 내부에서 _isSendingRequest가 false로 전환될 때 처리됨
        // 여기서는 별도로 SetAIAnalysisInProgress(false)를 호출하지 않음
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
        KeywordStatisticsWrapper wrapper = new KeywordStatisticsWrapper
        {
            KeywordLogs = _keywordLogs.ToArray()
        };
        string json = JsonUtility.ToJson(wrapper, true);
        File.WriteAllText(_statisticsFilePath, json);
        AssetDatabase.Refresh();
    }
}