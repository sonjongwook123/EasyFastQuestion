using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Text;

public class QuestionDetailWindow : EditorWindow
{
    private QuestionListTabHandler.QuestionEntry _currentEntry;
    private QuestionListTabHandler _questionListHandler;
    private GeminiChatGPTIntegrationEditor _parentEditorWindow;

    private Vector2 _memoScrollPos;
    private Vector2 _chatHistoryScrollPos; // New scroll position for chat history
    private string _newMemoText = "";
    private string _chatHistoryText = ""; // New field for combined chat history

    private const int MemosPerPage = 3;
    private int _currentMemoPage = 0;

    public static void ShowWindow(QuestionListTabHandler.QuestionEntry entry, QuestionListTabHandler handler, GeminiChatGPTIntegrationEditor parentEditor)
    {
        QuestionDetailWindow window = GetWindow<QuestionDetailWindow>("질문 상세");
        window._currentEntry = entry;
        window._questionListHandler = handler;
        window._parentEditorWindow = parentEditor;
        window._currentMemoPage = 0;
        window.UpdateChatHistoryText(); // Initialize chat history text
        window.ShowUtility();
        window.Focus();
    }

    private void UpdateChatHistoryText()
    {
        StringBuilder chatText = new StringBuilder();
        chatText.AppendLine($"<color=#3366FF><b>[나]</b></color> {_currentEntry.Question}");
        chatText.AppendLine();
        chatText.AppendLine($"<color=#008000><b>[{_currentEntry.ServiceType}]</b></color> {_currentEntry.Answer}");
        _chatHistoryText = chatText.ToString();
    }

    private void OnGUI()
    {
        if (_currentEntry == null || _questionListHandler == null || _parentEditorWindow == null)
        {
            EditorGUILayout.HelpBox("표시할 질문 정보가 없습니다. 창을 닫고 다시 시도해주세요.", MessageType.Error);
            return;
        }

        EditorGUILayout.LabelField("📚 질문 상세 정보", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("대화 내역", EditorStyles.boldLabel);
        _chatHistoryScrollPos = EditorGUILayout.BeginScrollView(_chatHistoryScrollPos, GUILayout.MinHeight(450));
        GUIStyle chatStyle = new GUIStyle(EditorStyles.textArea);
        chatStyle.richText = true;
        float textHeight = chatStyle.CalcHeight(new GUIContent(_chatHistoryText), position.width - 40);
        EditorGUILayout.SelectableLabel(_chatHistoryText, chatStyle, GUILayout.Height(textHeight));
        EditorGUILayout.EndScrollView();
        EditorGUILayout.Space(10);

        bool newIsImportant = EditorGUILayout.ToggleLeft("중요 표시:", _currentEntry.IsImportant, GUILayout.ExpandWidth(true));
        if (newIsImportant != _currentEntry.IsImportant)
        {
            _currentEntry.IsImportant = newIsImportant;
            _questionListHandler.SaveQuestions();
            _parentEditorWindow.Repaint();
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(10);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("📝 메모", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);

        int totalMemos = _currentEntry.Memos?.Count ?? 0;
        int startIndex = _currentMemoPage * MemosPerPage;
        List<MemoEntry> displayedMemoEntries = _currentEntry.Memos?.Skip(startIndex).Take(MemosPerPage).ToList() ?? new List<MemoEntry>();

        if (totalMemos == 0)
        {
            EditorGUILayout.HelpBox("아직 작성된 메모가 없습니다.", MessageType.Info);
        }
        else
        {
            _memoScrollPos = EditorGUILayout.BeginScrollView(_memoScrollPos, GUILayout.ExpandHeight(true));
            foreach (var memo in displayedMemoEntries)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.SelectableLabel($"[{memo.Timestamp:yyyy-MM-dd HH:mm:ss}]", EditorStyles.boldLabel);
                EditorGUILayout.SelectableLabel(memo.Content, EditorStyles.wordWrappedLabel);
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(5);
            }
            EditorGUILayout.EndScrollView();
        }

        int totalPages = (totalMemos + MemosPerPage - 1) / MemosPerPage;
        if (totalPages > 1)
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUI.enabled = (_currentMemoPage > 0);
            if (GUILayout.Button("◀ 이전 메모", GUILayout.Width(100)))
            {
                _currentMemoPage--;
                this.Repaint();
            }
            GUI.enabled = true;
            EditorGUILayout.LabelField($"페이지 {_currentMemoPage + 1} / {totalPages}", GUILayout.Width(100), GUILayout.ExpandWidth(false));
            GUI.enabled = (_currentMemoPage < totalPages - 1);
            if (GUILayout.Button("다음 메모 ▶", GUILayout.Width(100)))
            {
                _currentMemoPage++;
                this.Repaint();
            }
            GUI.enabled = true;
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("새 메모 추가:", EditorStyles.boldLabel);
        _newMemoText = EditorGUILayout.TextArea(_newMemoText, GUILayout.MinHeight(40));
        if (GUILayout.Button("메모 추가", GUILayout.Height(25)))
        {
            if (!string.IsNullOrWhiteSpace(_newMemoText))
            {
                _currentEntry.Memos.Add(new MemoEntry(_newMemoText, DateTime.Now));
                _newMemoText = "";
                _questionListHandler.SaveQuestions();
                _parentEditorWindow.Repaint();
                this.Repaint();
                _currentMemoPage = (totalMemos + 1 + MemosPerPage - 1) / MemosPerPage - 1;
                GUIUtility.ExitGUI();
            }
            else
            {
                EditorUtility.DisplayDialog("경고", "추가할 메모 내용을 입력해주세요.", "확인");
            }
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(10);

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("삭제", GUILayout.Width(80), GUILayout.Height(30)))
        {
            if (EditorUtility.DisplayDialog("질문 삭제 확인", "이 질문을 정말 삭제하시겠습니까?", "삭제", "취소"))
            {
                _questionListHandler.RemoveQuestion(_currentEntry);
                _questionListHandler.SaveQuestions();
                _parentEditorWindow.Repaint();
                this.Close();
            }
        }
        if (GUILayout.Button("닫기", GUILayout.Width(80), GUILayout.Height(30)))
        {
            this.Close();
        }
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
    }
}