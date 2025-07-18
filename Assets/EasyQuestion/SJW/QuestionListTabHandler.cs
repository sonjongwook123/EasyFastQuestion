using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System;

[System.Serializable]
public class QuestionListTabHandler
{
    private GeminiChatGPTIntegrationEditor _parentWindow;
    public List<QuestionEntry> _questions = new List<QuestionEntry>();
    private Vector2 _scrollPos;
    private string _historyFilePath;
    private string _scriptFolderPath;

    private int _selectedCategoryTab = 0; // 0: 전체, 1: 중요, 2: Gemini, 3: ChatGPT
    private string[] _categoryTabNames = { "전체", "중요", "Gemini", "ChatGPT" };
    private string _searchQuery = "";

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
                _scriptFolderPath = Application.dataPath + "/Editor"; // fallback
                _historyFilePath = Path.Combine(_scriptFolderPath, "question_history.json");
            }
            LoadQuestions();
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

        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
        EditorGUILayout.LabelField("검색:", GUILayout.Width(40));
        string newSearchQuery = EditorGUILayout.TextField(_searchQuery, GUILayout.ExpandWidth(true));
        if (newSearchQuery != _searchQuery)
        {
            _searchQuery = newSearchQuery;
            _parentWindow?.Repaint();
        }
        if (GUILayout.Button("초기화", GUILayout.Width(60)))
        {
            _searchQuery = "";
            _parentWindow?.Repaint();
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(5);

        int newSelectedCategoryTab = GUILayout.Toolbar(_selectedCategoryTab, _categoryTabNames);
        if (newSelectedCategoryTab != _selectedCategoryTab)
        {
            _selectedCategoryTab = newSelectedCategoryTab;
            _parentWindow?.Repaint();
        }
        EditorGUILayout.Space(10);

        List<QuestionEntry> filteredQuestions = FilterAndSearchQuestions();

        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

        if (!filteredQuestions.Any())
        {
            EditorGUILayout.HelpBox("조건에 맞는 질문 내역이 없습니다.",MessageType.Info);
        }
        else
        {
            foreach (var entry in filteredQuestions)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                
                EditorGUILayout.BeginHorizontal();
                bool newIsImportant = EditorGUILayout.ToggleLeft("", entry.IsImportant, GUILayout.Width(20));
                if (newIsImportant != entry.IsImportant)
                {
                    entry.IsImportant = newIsImportant;
                    SaveQuestions();
                    _parentWindow?.Repaint();
                }
                EditorGUILayout.LabelField($"[{entry.Timestamp:yyyy-MM-dd HH:mm}] {entry.ServiceType} 질문:", EditorStyles.boldLabel);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.SelectableLabel(entry.Question, EditorStyles.wordWrappedLabel);
                
                EditorGUILayout.Space(5);
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("상세 보기", GUILayout.Width(100)))
                {
                    QuestionDetailWindow.ShowWindow(entry, this, _parentWindow);
                }
                if (GUILayout.Button("삭제", GUILayout.Width(60)))
                {
                    if (EditorUtility.DisplayDialog("질문 삭제 확인", "이 질문을 정말 삭제하시겠습니까?", "삭제", "취소"))
                    {
                        RemoveQuestion(entry);
                        SaveQuestions();
                        _parentWindow?.Repaint();
                        GUIUtility.ExitGUI(); // 삭제 후 OnGUI 재진입 방지
                    }
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(5);
            }
        }
        EditorGUILayout.EndScrollView();
    }

    private List<QuestionEntry> FilterAndSearchQuestions()
    {
        IEnumerable<QuestionEntry> query = _questions;

        switch (_selectedCategoryTab)
        {
            case 1: // 중요
                query = query.Where(q => q.IsImportant);
                break;
            case 2: // Gemini
                query = query.Where(q => q.ServiceType == AiServiceType.Gemini);
                break;
            case 3: // ChatGPT
                query = query.Where(q => q.ServiceType == AiServiceType.ChatGPT);
                break;
        }

        if (!string.IsNullOrWhiteSpace(_searchQuery))
        {
            string lowerSearchQuery = _searchQuery.ToLower();
            query = query.Where(q =>
                q.Question.ToLower().Contains(lowerSearchQuery) ||
                q.Answer.ToLower().Contains(lowerSearchQuery) ||
                q.Memos.Any(m => m.Content.ToLower().Contains(lowerSearchQuery))
            );
        }

        return query.ToList();
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
        // 메모 리스트가 null인 경우 초기화
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
        AssetDatabase.Refresh(); // Unity 에디터에 변경 사항 반영
    }
}