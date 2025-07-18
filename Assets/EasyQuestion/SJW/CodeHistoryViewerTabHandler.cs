// Editor/CodeHistoryViewerTabHandler.cs
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;

[System.Serializable]
public class CodeHistoryViewerTabHandler
{
    private Vector2 _scrollPos;
    private List<CodeChangeEntry> _codeHistory = new List<CodeChangeEntry>();
    private string _historyFilePath;
    private string _scriptFolderPath;

    public CodeHistoryViewerTabHandler() { }

    public void Initialize(EditorWindow parentWindow)
    {
        if (string.IsNullOrEmpty(_scriptFolderPath))
        {
            string scriptPath = AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(parentWindow));
            _scriptFolderPath = Path.GetDirectoryName(scriptPath);
            _historyFilePath = Path.Combine(_scriptFolderPath, "code_history.json");
            LoadHistory();
        }
    }

    public void RecordCodeChange(string fileName, string timestamp, string originalCode, string modifiedCode, string scriptPath)
    {
        if (_codeHistory == null)
        {
            _codeHistory = new List<CodeChangeEntry>();
        }

        CodeChangeEntry newEntry = new CodeChangeEntry
        {
            FileName = fileName,
            Timestamp = timestamp,
            OriginalCode = originalCode,
            ModifiedCode = modifiedCode,
            ScriptPath = scriptPath
        };

        _codeHistory.Insert(0, newEntry); 
        SaveHistory();

        _scrollPos.y = 0; 
    }

    private void LoadHistory()
    {
        if (File.Exists(_historyFilePath))
        {
            string json = File.ReadAllText(_historyFilePath);
            try
            {
                CodeHistoryWrapper wrapper = JsonUtility.FromJson<CodeHistoryWrapper>(json);
                if (wrapper != null && wrapper.Entries != null)
                {
                    _codeHistory = new List<CodeChangeEntry>(wrapper.Entries);
                }
                else
                {
                    _codeHistory = new List<CodeChangeEntry>();
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to load code history: {e.Message}");
                _codeHistory = new List<CodeChangeEntry>();
            }
        }
        else
        {
            _codeHistory = new List<CodeChangeEntry>();
        }
    }

    private void SaveHistory()
    {
        if (_codeHistory == null) _codeHistory = new List<CodeChangeEntry>(); 
        CodeHistoryWrapper wrapper = new CodeHistoryWrapper { Entries = _codeHistory.ToArray() };
        string json = JsonUtility.ToJson(wrapper, true); 
        File.WriteAllText(_historyFilePath, json);
    }

    public void OnGUI(float editorWindowWidth, float editorWindowHeight)
    {
        // ⭐ 라벨 변경
        EditorGUILayout.LabelField("📚 코드 변경 내역", EditorStyles.boldLabel); 
        EditorGUILayout.Space();

        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.ExpandHeight(true));

        if (_codeHistory != null && _codeHistory.Count > 0)
        {
            foreach (var entry in _codeHistory)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"파일명: {entry.FileName}", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"시간: {entry.Timestamp}");
                EditorGUILayout.LabelField($"경로: {entry.ScriptPath}");
                EditorGUILayout.Space(5);

                EditorGUILayout.LabelField("수정된 코드:", EditorStyles.boldLabel);
                EditorGUILayout.SelectableLabel(entry.ModifiedCode, EditorStyles.textArea, GUILayout.MinHeight(50));
                
                EditorGUILayout.Space(5);
                if (!string.IsNullOrEmpty(entry.OriginalCode))
                {
                    EditorGUILayout.LabelField("기존 코드:", EditorStyles.boldLabel);
                    EditorGUILayout.SelectableLabel(entry.OriginalCode, EditorStyles.textArea, GUILayout.MinHeight(50));
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(10);
            }
        }
        else
        {
            EditorGUILayout.HelpBox("코드 변경 내역이 없습니다.", MessageType.Info);
        }

        EditorGUILayout.EndScrollView();

        if (GUILayout.Button("내역 지우기", GUILayout.Height(30)))
        {
            if (EditorUtility.DisplayDialog("내역 지우기", "정말로 모든 코드 변경 내역을 지우시겠습니까?", "예", "아니오"))
            {
                _codeHistory.Clear();
                SaveHistory();
            }
        }
    }

    [System.Serializable]
    private class CodeChangeEntry
    {
        public string FileName;
        public string Timestamp;
        public string OriginalCode;
        public string ModifiedCode;
        public string ScriptPath;
    }

    [System.Serializable]
    private class CodeHistoryWrapper
    {
        public CodeChangeEntry[] Entries;
    }
}