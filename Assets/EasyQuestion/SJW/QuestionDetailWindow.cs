// Editor/QuestionDetailWindow.cs
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

public class QuestionDetailWindow : EditorWindow
{
    private QuestionListTabHandler.QuestionEntry _currentEntry;
    private QuestionListTabHandler _parentHandler; // 부모 핸들러 참조
    private Vector2 _scrollPos;
    private string _newMemoText = "";

    public static void ShowWindow(QuestionListTabHandler.QuestionEntry entry, QuestionListTabHandler parentHandler)
    {
        QuestionDetailWindow window = GetWindow<QuestionDetailWindow>("질문 상세");
        window._currentEntry = entry;
        window._parentHandler = parentHandler;
        window.minSize = new Vector2(400, 500);
        window.Show();
    }

    private void OnGUI()
    {
        if (_currentEntry == null)
        {
            EditorGUILayout.HelpBox("표시할 질문이 없습니다.", MessageType.Info);
            return;
        }

        EditorGUILayout.LabelField("질문 상세 정보", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.ExpandHeight(true));

        // 질문 정보 표시
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField($"시간: {_currentEntry.Timestamp}", EditorStyles.miniLabel);
        EditorGUILayout.LabelField($"AI 서비스: {_currentEntry.AiType}", EditorStyles.miniLabel);
        EditorGUILayout.LabelField($"중요: {(_currentEntry.IsImportant ? "⭐ 예" : "아니오")}", EditorStyles.miniLabel);
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("질문:", EditorStyles.boldLabel);
        EditorGUILayout.SelectableLabel(_currentEntry.Question, EditorStyles.wordWrappedLabel);
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("답변:", EditorStyles.boldLabel);
        EditorGUILayout.SelectableLabel(_currentEntry.Answer, EditorStyles.wordWrappedLabel);
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(10);

        // 메모 섹션
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("관련 메모", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);

        if (_currentEntry.Memos != null && _currentEntry.Memos.Count > 0)
        {
            for (int i = 0; i < _currentEntry.Memos.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.SelectableLabel(_currentEntry.Memos[i], EditorStyles.wordWrappedLabel);
                if (GUILayout.Button("삭제", GUILayout.Width(50)))
                {
                    _currentEntry.Memos.RemoveAt(i);
                    _parentHandler.UpdateQuestionEntry(_currentEntry); // 부모 핸들러를 통해 저장
                    GUIUtility.ExitGUI(); // 삭제 후 UI 갱신을 위해 즉시 종료
                }
                EditorGUILayout.EndHorizontal();
            }
        }
        else
        {
            EditorGUILayout.HelpBox("추가된 메모가 없습니다.", MessageType.Info);
        }

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("새 메모 추가:", EditorStyles.boldLabel);
        _newMemoText = EditorGUILayout.TextArea(_newMemoText, GUILayout.MinHeight(50));

        if (GUILayout.Button("메모 추가", GUILayout.Height(30)))
        {
            if (!string.IsNullOrEmpty(_newMemoText))
            {
                _parentHandler.AddMemoToQuestion(_currentEntry, _newMemoText);
                _newMemoText = ""; // 입력 필드 초기화
            }
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.EndScrollView();

        // 창 닫기 버튼
        if (GUILayout.Button("닫기", GUILayout.Height(30)))
        {
            Close();
        }
    }
}