using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System;
using System.Text;

[System.Serializable]
public class QuestionListTabHandler
{
    private GeminiChatGPTIntegrationEditor _parentWindow;
    public List<QuestionEntry> _questions = new List<QuestionEntry>();
    private Vector2 _scrollPos;
    private string _historyFilePath;
    private string _scriptFolderPath;

    private int _selectedCategoryTab = 0; 
    private string[] _categoryTabNames = { "전체", "중요", "Gemini", "ChatGPT" };
    private string _searchQuery = "";

    private const int QuestionsPerPage = 5;
    private int _questionsCurrentPage = 0; 

    private Color _questionTextColor = Color.yellow; 
    private Color _answerTextColor = Color.green;   

    private const string QuestionTextColorPrefKey = "QuestionTextColor";
    private const string AnswerTextColorPrefKey = "AnswerTextColor";


    [System.Serializable]
    public class QuestionEntry
    {
        public string Question;
        public string Answer;
        public AiServiceType ServiceType;
        public List<MemoEntry> Memos;
        public DateTime Timestamp;
        public bool IsImportant;

        public QuestionEntry(string question, string answer, AiServiceType serviceType, DateTime timestamp)
        {
            Question = question;
            Answer = answer;
            ServiceType = serviceType;
            Memos = new List<MemoEntry>();
            Timestamp = timestamp;
            IsImportant = false;
        }
    }

    [System.Serializable]
    private class QuestionHistoryWrapper
    {
        public QuestionEntry[] Questions;
    }

    public QuestionListTabHandler() { }

    public void Initialize(EditorWindow parentWindow)
    {
        _parentWindow = parentWindow as GeminiChatGPTIntegrationEditor;
        if (string.IsNullOrEmpty(_scriptFolderPath))
        {
            string[] guids = AssetDatabase.FindAssets("t:Script " + typeof(QuestionListTabHandler).Name);
            if (guids.Length > 0)
            {
                string scriptPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                _scriptFolderPath = Path.GetDirectoryName(scriptPath);
                _historyFilePath = Path.Combine(_scriptFolderPath, "question_history.json");
            }
            else
            {
                Debug.LogError("QuestionListTabHandler.cs 파일을 찾을 수 없습니다. 경로 설정을 수동으로 확인해주세요.");
                _scriptFolderPath = Application.dataPath + "/Editor"; 
                _historyFilePath = Path.Combine(_scriptFolderPath, "question_history.json");
            }
            LoadQuestions();
            LoadColors(); 
        }
    }

    public void AddQuestion(string question, string answer, AiServiceType serviceType)
    {
        _questions.Insert(0, new QuestionEntry(question, answer, serviceType, DateTime.Now));
        SaveQuestions();
    }

    public void RemoveQuestion(QuestionEntry entryToRemove)
    {
        _questions.Remove(entryToRemove);
    }

    public void OnGUI(float editorWindowWidth, float editorWindowHeight)
    {
        EditorGUILayout.LabelField("📚 질문 리스트", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("검색 및 필터", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("검색:", GUILayout.Width(40));
        string newSearchQuery = EditorGUILayout.TextField(_searchQuery, GUILayout.ExpandWidth(true));
        if (newSearchQuery != _searchQuery)
        {
            _searchQuery = newSearchQuery;
            _questionsCurrentPage = 0;
            _parentWindow?.Repaint();
        }
        if (GUILayout.Button("초기화", GUILayout.Width(60)))
        {
            _searchQuery = "";
            _questionsCurrentPage = 0; 
            _parentWindow?.Repaint();
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(5);

        int newSelectedCategoryTab = GUILayout.Toolbar(_selectedCategoryTab, _categoryTabNames);
        if (newSelectedCategoryTab != _selectedCategoryTab)
        {
            _selectedCategoryTab = newSelectedCategoryTab;
            _questionsCurrentPage = 0;
            _parentWindow?.Repaint();
        }
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(10);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("텍스트 색상 설정", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        _questionTextColor = EditorGUILayout.ColorField("질문 텍스트 색상:", _questionTextColor);
        _answerTextColor = EditorGUILayout.ColorField("답변 텍스트 색상:", _answerTextColor);
        if (EditorGUI.EndChangeCheck())
        {
            SaveColors();
            _parentWindow?.Repaint();
        }
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(10);


        List<QuestionEntry> filteredQuestions = FilterAndSearchQuestions();
        int totalQuestionPages = Mathf.CeilToInt((float)filteredQuestions.Count / QuestionsPerPage);

        if (totalQuestionPages > 1) 
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUI.enabled = (_questionsCurrentPage < totalQuestionPages - 1);
            if (GUILayout.Button("이전 질문 ◀", GUILayout.Width(100)))
            {
                _questionsCurrentPage++;
                _parentWindow?.Repaint();
            }
            GUI.enabled = (_questionsCurrentPage > 0);
            if (GUILayout.Button("다음 질문 ▶", GUILayout.Width(100)))
            {
                _questionsCurrentPage--;
                _parentWindow?.Repaint();
            }
            GUI.enabled = true;
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(5);
        }


        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

        if (!filteredQuestions.Any())
        {
            EditorGUILayout.HelpBox("조건에 맞는 질문 내역이 없습니다.", MessageType.Info);
        }
        else
        {
            List<QuestionEntry> currentQuestions = filteredQuestions
                .Skip(_questionsCurrentPage * QuestionsPerPage)
                .Take(QuestionsPerPage)
                .ToList();

            foreach (var entry in currentQuestions)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"<b>질문:</b> <color=#{ColorUtility.ToHtmlStringRGB(_questionTextColor)}>{entry.Question}</color>", GetRichTextStyle());
                GUILayout.FlexibleSpace();
                bool newIsImportant = GUILayout.Toggle(entry.IsImportant, new GUIContent(entry.IsImportant ? "★ 중요" : "☆ 중요", "이 질문을 중요 표시합니다."), GUILayout.Width(60));
                if (newIsImportant != entry.IsImportant)
                {
                    entry.IsImportant = newIsImportant;
                    SaveQuestions();
                    _parentWindow?.Repaint();
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.LabelField($"<b>답변 ({entry.ServiceType}):</b> <color=#{ColorUtility.ToHtmlStringRGB(_answerTextColor)}>{entry.Answer}</color>", GetRichTextStyle());
                EditorGUILayout.LabelField($"<size=10><color=grey>{entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss")}</color></size>", GetRichTextStyle());

                EditorGUILayout.Space(5);
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("상세 보기", GUILayout.Width(100), GUILayout.Height(25)))
                {
                    QuestionDetailWindow.ShowWindow(entry, this, _parentWindow);
                }
                if (GUILayout.Button("삭제", GUILayout.Width(60), GUILayout.Height(25)))
                {
                    if (EditorUtility.DisplayDialog("질문 삭제 확인", "이 질문을 정말 삭제하시겠습니까?", "삭제", "취소"))
                    {
                        RemoveQuestion(entry);
                        SaveQuestions();
                        _parentWindow?.Repaint();
                        break;
                    }
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(10);
            }
        }
        EditorGUILayout.EndScrollView();
    }

    private List<QuestionEntry> FilterAndSearchQuestions()
    {
        IEnumerable<QuestionEntry> query = _questions.AsEnumerable();

        switch (_selectedCategoryTab)
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
            default: 
                break;
        }

        if (!string.IsNullOrWhiteSpace(_searchQuery))
        {
            string lowerSearchQuery = _searchQuery.ToLower();
            query = query.Where(q =>
                q.Question.ToLower().Contains(lowerSearchQuery) ||
                q.Answer.ToLower().Contains(lowerSearchQuery) ||
                (q.Memos != null && q.Memos.Any(m => m.Content.ToLower().Contains(lowerSearchQuery)))
            );
        }

        return query.OrderByDescending(q => q.Timestamp).ToList();
    }

    public void LoadQuestions()
    {
        if (File.Exists(_historyFilePath))
        {
            string json = File.ReadAllText(_historyFilePath);
            try
            {
                QuestionHistoryWrapper wrapper = JsonUtility.FromJson<QuestionHistoryWrapper>(json);
                if (wrapper != null && wrapper.Questions != null)
                {
                    _questions = new List<QuestionEntry>(wrapper.Questions);
                }
                else
                {
                    _questions = new List<QuestionEntry>();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to load question history: {e.Message}");
                _questions = new List<QuestionEntry>();
            }
        }
        else
        {
            _questions = new List<QuestionEntry>();
        }
        
        foreach (var q in _questions)
        {
            if (q.Memos == null) q.Memos = new List<MemoEntry>();
        }
    }

    public void SaveQuestions()
    {
        if (_questions == null) _questions = new List<QuestionEntry>();
        QuestionHistoryWrapper wrapper = new QuestionHistoryWrapper { Questions = _questions.ToArray() };
        string json = JsonUtility.ToJson(wrapper, true);
        File.WriteAllText(_historyFilePath, json);
        AssetDatabase.Refresh(); 
    }

    private void LoadColors()
    {
        string questionColorJson = EditorPrefs.GetString(QuestionTextColorPrefKey, JsonUtility.ToJson(Color.yellow));
        string answerColorJson = EditorPrefs.GetString(AnswerTextColorPrefKey, JsonUtility.ToJson(Color.green));

        try
        {
            _questionTextColor = JsonUtility.FromJson<Color>(questionColorJson);
            _answerTextColor = JsonUtility.FromJson<Color>(answerColorJson);
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to load text colors: {e.Message}. Resetting to defaults.");
            _questionTextColor = Color.yellow;
            _answerTextColor = Color.green;
        }
    }

    private void SaveColors()
    {
        EditorPrefs.SetString(QuestionTextColorPrefKey, JsonUtility.ToJson(_questionTextColor));
        EditorPrefs.SetString(AnswerTextColorPrefKey, JsonUtility.ToJson(_answerTextColor));
    }

    private GUIStyle GetRichTextStyle()
    {
        GUIStyle style = new GUIStyle(EditorStyles.label);
        style.richText = true;
        style.wordWrap = true;
        return style;
    }
}