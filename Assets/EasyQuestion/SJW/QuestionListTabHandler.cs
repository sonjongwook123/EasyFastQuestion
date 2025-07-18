// Editor/QuestionListTabHandler.cs
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;

[System.Serializable]
public class QuestionListTabHandler
{
    private Vector2 _scrollPos;
    private List<QuestionEntry> _questions = new List<QuestionEntry>();
    private string _historyFilePath;
    private string _scriptFolderPath;

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

    public void AddQuestion(string question)
    {
        if (_questions == null)
        {
            _questions = new List<QuestionEntry>();
        }

        QuestionEntry newEntry = new QuestionEntry
        {
            Question = question,
            Timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        };

        _questions.Insert(0, newEntry); // 최신 질문이 맨 위에 오도록
        SaveHistory();

        _scrollPos.y = 0; // 스크롤을 맨 위로 이동
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

        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.ExpandHeight(true));

        if (_questions != null && _questions.Count > 0)
        {
            foreach (var entry in _questions)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"시간: {entry.Timestamp}", EditorStyles.miniLabel);
                EditorGUILayout.LabelField(entry.Question, EditorStyles.wordWrappedLabel);
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
    private class QuestionEntry
    {
        public string Question;
        public string Timestamp;
    }

    [System.Serializable]
    private class QuestionHistoryWrapper
    {
        public QuestionEntry[] Entries;
    }
}