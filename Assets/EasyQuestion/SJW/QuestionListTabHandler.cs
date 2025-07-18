// Editor/QuestionListTabHandler.cs
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;

// ⭐ AI 서비스 타입 enum 정의
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

    private int _selectedSubTabIndex = 0; // ⭐ 서브 탭 인덱스
    private string[] _subTabNames = { "전체 질문", "제미니 질문", "지피티 질문", "중요 질문" }; // ⭐ 서브 탭 이름

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

    // ⭐ answer와 aiType 파라미터 추가
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
            IsImportant = false, // 기본값은 중요하지 않음
            Memos = new List<string>() // 빈 메모 리스트로 초기화
        };

        _questions.Insert(0, newEntry); // 최신 질문이 맨 위에 오도록
        SaveHistory();

        _scrollPos.y = 0; // 스크롤을 맨 위로 이동
    }

    // ⭐ 중요 상태 토글 메서드
    public void ToggleImportant(QuestionEntry entry)
    {
        entry.IsImportant = !entry.IsImportant;
        SaveHistory();
        EditorWindow.GetWindow<GeminiChatGPTIntegrationEditor>().Repaint(); // UI 갱신
    }

    // ⭐ 메모 추가 메서드
    public void AddMemoToQuestion(QuestionEntry entry, string memo)
    {
        if (entry.Memos == null)
        {
            entry.Memos = new List<string>();
        }
        entry.Memos.Add(memo);
        SaveHistory();
        EditorWindow.GetWindow<GeminiChatGPTIntegrationEditor>().Repaint(); // UI 갱신
    }

    // ⭐ 질문 업데이트 메서드 (상세보기에서 메모 수정 시 사용)
    public void UpdateQuestionEntry(QuestionEntry updatedEntry)
    {
        int index = _questions.FindIndex(q => q.Timestamp == updatedEntry.Timestamp && q.Question == updatedEntry.Question); // 고유 식별자 필요
        if (index != -1)
        {
            _questions[index] = updatedEntry;
            SaveHistory();
            EditorWindow.GetWindow<GeminiChatGPTIntegrationEditor>().Repaint();
        }
    }


    private void LoadHistory()
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
                    // ⭐ 이전 버전에서 추가되지 않은 필드 초기화
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
        string json = JsonUtility.ToJson(wrapper, true); // true for pretty print
        File.WriteAllText(_historyFilePath, json);
    }

    public void OnGUI(float editorWindowWidth, float editorWindowHeight)
    {
        EditorGUILayout.LabelField("❓ 질문 리스트", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // ⭐ 서브 탭 바
        _selectedSubTabIndex = GUILayout.Toolbar(_selectedSubTabIndex, _subTabNames);
        EditorGUILayout.Space(10);

        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.ExpandHeight(true));

        List<QuestionEntry> displayedQuestions = new List<QuestionEntry>();

        // ⭐ 선택된 서브 탭에 따라 질문 필터링
        switch (_selectedSubTabIndex)
        {
            case 0: // 전체 질문
                displayedQuestions = _questions;
                break;
            case 1: // 제미니 질문
                displayedQuestions = _questions.Where(q => q.AiType == AiServiceType.Gemini).ToList();
                break;
            case 2: // 지피티 질문
                displayedQuestions = _questions.Where(q => q.AiType == AiServiceType.ChatGPT).ToList();
                break;
            case 3: // 중요 질문
                displayedQuestions = _questions.Where(q => q.IsImportant).ToList();
                break;
        }

        if (displayedQuestions != null && displayedQuestions.Count > 0)
        {
            foreach (var entry in displayedQuestions)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"시간: {entry.Timestamp} | AI: {entry.AiType} {(entry.IsImportant ? "⭐" : "")}", EditorStyles.miniLabel);
                EditorGUILayout.LabelField(entry.Question, EditorStyles.wordWrappedLabel);
                
                EditorGUILayout.BeginHorizontal();
                // ⭐ 중요 버튼
                if (GUILayout.Button(entry.IsImportant ? "⭐ 중요 해제" : "⭐ 중요", GUILayout.Width(100)))
                {
                    ToggleImportant(entry);
                }
                // ⭐ 상세보기 버튼
                if (GUILayout.Button("상세보기", GUILayout.Width(100)))
                {
                    QuestionDetailWindow.ShowWindow(entry, this); // 현재 핸들러 인스턴스를 전달
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(5);
            }
        }
        else
        {
            EditorGUILayout.HelpBox("아직 질문 내역이 없습니다.", MessageType.Info);
        }

        EditorGUILayout.EndScrollView();

        if (GUILayout.Button("질문 내역 지우기", GUILayout.Height(30)))
        {
            if (EditorUtility.DisplayDialog("내역 지우기", "정말로 모든 질문 내역을 지우시겠습니까?", "예", "아니오"))
            {
                _questions.Clear();
                SaveHistory();
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