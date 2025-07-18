using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System;

public enum AiServiceType
{
    None,
    Gemini,
    ChatGPT
}

[System.Serializable]
public class QuestionListTabHandler
{
    private Vector2 _scrollPos;
    private List<QuestionEntry> _questions = new List<QuestionEntry>();
    private string _historyFilePath;
    private string _scriptFolderPath;

    private int _selectedSubTabIndex = 0;
    private string[] _subTabNames = { "전체 질문", "제미니 질문", "지피티 질문", "중요 질문" };

    private string _searchQuery = "";
    private int _currentPage = 0;
    private const int ItemsPerPage = 20;

    public QuestionListTabHandler() { }

    public void Initialize(EditorWindow parentWindow)
    {
        if (string.IsNullOrEmpty(_scriptFolderPath))
        {
            string scriptPath = AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(parentWindow));
            _scriptFolderPath = Path.GetDirectoryName(scriptPath);
            _historyFilePath = Path.Combine(_scriptFolderPath, "question_history.json");
            LoadHistory();
        }
    }

    public void AddQuestion(string question, string answer, AiServiceType aiType)
    {
        if (_questions == null)
        {
            _questions = new List<QuestionEntry>();
        }

        QuestionEntry newEntry = new QuestionEntry
        {
            Question = question,
            Answer = answer,
            Timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            AiType = aiType,
            IsImportant = false,
            Memos = new List<string>()
        };

        _questions.Insert(0, newEntry);
        SaveHistory();

        _currentPage = 0;
        _scrollPos.y = 0;
    }

    public void ToggleImportant(QuestionEntry entry)
    {
        entry.IsImportant = !entry.IsImportant;
        SaveHistory();
        EditorWindow.GetWindow<GeminiChatGPTIntegrationEditor>().Repaint();
    }

    public void AddMemoToQuestion(QuestionEntry entry, string memo)
    {
        if (entry.Memos == null)
        {
            entry.Memos = new List<string>();
        }
        entry.Memos.Add(memo);
        SaveHistory();
    }

    public void UpdateQuestionEntry(QuestionEntry updatedEntry)
    {
        int index = _questions.FindIndex(q => q.Timestamp == updatedEntry.Timestamp && q.Question == updatedEntry.Question);
        if (index != -1)
        {
            _questions[index] = updatedEntry;
            SaveHistory();
            EditorWindow.GetWindow<GeminiChatGPTIntegrationEditor>().Repaint();
        }
    }

    public void LoadHistory()
    {
        if (File.Exists(_historyFilePath))
        {
            string json = File.ReadAllText(_historyFilePath);
            try
            {
                QuestionHistoryWrapper wrapper = JsonUtility.FromJson<QuestionHistoryWrapper>(json);
                if (wrapper != null && wrapper.Entries != null)
                {
                    _questions = new List<QuestionEntry>(wrapper.Entries);
                    foreach (var q in _questions)
                    {
                        if (q.Answer == null) q.Answer = "";
                        if (q.Memos == null) q.Memos = new List<string>();
                    }
                }
                else
                {
                    _questions = new List<QuestionEntry>();
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to load question history: {e.Message}");
                _questions = new List<QuestionEntry>();
            }
        }
        else
        {
            _questions = new List<QuestionEntry>();
        }
    }

    private void SaveHistory()
    {
        if (_questions == null) _questions = new List<QuestionEntry>();
        QuestionHistoryWrapper wrapper = new QuestionHistoryWrapper { Entries = _questions.ToArray() };
        string json = JsonUtility.ToJson(wrapper, true);
        File.WriteAllText(_historyFilePath, json);
    }

    public void OnGUI(float editorWindowWidth, float editorWindowHeight)
    {
        EditorGUILayout.LabelField("❓ 질문 리스트", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        _selectedSubTabIndex = GUILayout.Toolbar(_selectedSubTabIndex, _subTabNames);
        EditorGUILayout.Space(10);

        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        _searchQuery = EditorGUILayout.TextField("검색:", _searchQuery, EditorStyles.toolbarSearchField);
        if (GUILayout.Button("X", EditorStyles.toolbarButton, GUILayout.Width(20)))
        {
            _searchQuery = "";
            GUI.FocusControl(null);
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(5);

        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.ExpandHeight(true));

        List<QuestionEntry> filteredQuestions = new List<QuestionEntry>();

        switch (_selectedSubTabIndex)
        {
            case 0:
                filteredQuestions = _questions;
                break;
            case 1:
                filteredQuestions = _questions.Where(q => q.AiType == AiServiceType.Gemini).ToList();
                break;
            case 2:
                filteredQuestions = _questions.Where(q => q.AiType == AiServiceType.ChatGPT).ToList();
                break;
            case 3:
                filteredQuestions = _questions.Where(q => q.IsImportant).ToList();
                break;
        }

        if (!string.IsNullOrEmpty(_searchQuery))
        {
            string lowerSearchQuery = _searchQuery.ToLower();
            filteredQuestions = filteredQuestions.Where(q =>
                q.Question.ToLower().Contains(lowerSearchQuery) ||
                q.Answer.ToLower().Contains(lowerSearchQuery) ||
                (q.Memos != null && q.Memos.Any(m => m.ToLower().Contains(lowerSearchQuery)))
            ).ToList();
        }

        int totalQuestions = filteredQuestions.Count;
        int totalPages = Mathf.CeilToInt((float)totalQuestions / ItemsPerPage);

        if (_currentPage >= totalPages && totalPages > 0)
        {
            _currentPage = totalPages - 1;
        }
        else if (totalPages == 0)
        {
            _currentPage = 0;
        }

        int startIndex = _currentPage * ItemsPerPage;
        List<QuestionEntry> displayedQuestions = filteredQuestions
            .Skip(startIndex)
            .Take(ItemsPerPage)
            .ToList();


        if (displayedQuestions != null && displayedQuestions.Count > 0)
        {
            foreach (var entry in displayedQuestions)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"시간: {entry.Timestamp} | AI: {entry.AiType} {(entry.IsImportant ? "⭐" : "")}", EditorStyles.miniLabel);
                EditorGUILayout.LabelField(entry.Question, EditorStyles.wordWrappedLabel);
                
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button(entry.IsImportant ? "⭐ 중요 해제" : "⭐ 중요", GUILayout.Width(100)))
                {
                    ToggleImportant(entry);
                }
                if (GUILayout.Button("상세보기", GUILayout.Width(100)))
                {
                    QuestionDetailWindow.ShowWindow(entry, this);
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(5);
            }
        }
        else
        {
            EditorGUILayout.HelpBox("아직 질문 내역이 없거나 검색 결과가 없습니다.", MessageType.Info);
        }

        EditorGUILayout.EndScrollView();

        if (totalPages > 1)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUI.enabled = (_currentPage > 0);
            if (GUILayout.Button("◀ 이전", GUILayout.Width(80)))
            {
                _currentPage--;
                _scrollPos.y = 0;
            }
            GUI.enabled = true;

            EditorGUILayout.LabelField($"{_currentPage + 1} / {totalPages}", EditorStyles.centeredGreyMiniLabel, GUILayout.Width(60));

            GUI.enabled = (_currentPage < totalPages - 1);
            if (GUILayout.Button("다음 ▶", GUILayout.Width(80)))
            {
                _currentPage++;
                _scrollPos.y = 0;
            }
            GUI.enabled = true;
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }


        if (GUILayout.Button("질문 내역 지우기", GUILayout.Height(30)))
        {
            if (EditorUtility.DisplayDialog("내역 지우기", "정말로 모든 질문 내역을 지우시겠습니까?", "예", "아니오"))
            {
                _questions.Clear();
                SaveHistory();
                _currentPage = 0;
            }
        }
    }

    [System.Serializable]
    public class QuestionEntry
    {
        public string Question;
        public string Answer;
        public string Timestamp;
        public AiServiceType AiType;
        public bool IsImportant;
        public List<string> Memos;
    }

    [System.Serializable]
    private class QuestionHistoryWrapper
    {
        public QuestionEntry[] Entries;
    }
}