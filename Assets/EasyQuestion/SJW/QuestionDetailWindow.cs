using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class QuestionDetailWindow : EditorWindow
{
    private QuestionListTabHandler.QuestionEntry _currentEntry;
    private QuestionListTabHandler _parentHandler;
    private Vector2 _questionAnswerScrollPos;
    private Vector2 _memoListScrollPos;
    private string _newMemoText = "";

    private int _currentMemoPage = 0;
    private const int MemosPerPage = 5;

    public static void ShowWindow(QuestionListTabHandler.QuestionEntry entry, QuestionListTabHandler parentHandler)
    {
        QuestionDetailWindow window = GetWindow<QuestionDetailWindow>("질문 상세");
        window._currentEntry = entry;
        window._parentHandler = parentHandler;
        window.minSize = new Vector2(400, 550);
        window.Show();
        
        window._questionAnswerScrollPos = Vector2.zero;
        window._memoListScrollPos = Vector2.zero;
        window._newMemoText = "";
        window._currentMemoPage = 0;

        window.Repaint();
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

        // 질문 및 답변 섹션
        EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.ExpandHeight(true));
        
        EditorGUILayout.LabelField($"시간: {_currentEntry.Timestamp}", EditorStyles.miniLabel);
        EditorGUILayout.LabelField($"AI 서비스: {_currentEntry.AiType}", EditorStyles.miniLabel);
        EditorGUILayout.LabelField($"중요: {(_currentEntry.IsImportant ? "⭐ 예" : "아니오")}", EditorStyles.miniLabel);
        EditorGUILayout.Space(5);

        _questionAnswerScrollPos = EditorGUILayout.BeginScrollView(_questionAnswerScrollPos, GUILayout.ExpandHeight(true));

        GUIStyle combinedTextStyle = new GUIStyle(EditorStyles.wordWrappedLabel);
        combinedTextStyle.normal.textColor = EditorStyles.label.normal.textColor;
        combinedTextStyle.padding = new RectOffset(5, 5, 5, 5);
        combinedTextStyle.richText = true;

        string combinedText = $"<color=white><b>질문:</b>\n{_currentEntry.Question}</color>\n\n<color=#ADD8E6><b>답변:</b>\n{_currentEntry.Answer}</color>";
        EditorGUILayout.SelectableLabel(combinedText, combinedTextStyle, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(10);

        // 메모 섹션
        EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Height(300)); // 메모 섹션 세로 크기 증가
        EditorGUILayout.LabelField("관련 메모", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);

        _memoListScrollPos = EditorGUILayout.BeginScrollView(_memoListScrollPos, GUILayout.ExpandHeight(true));

        List<string> allMemos = _currentEntry.Memos ?? new List<string>();
        int totalMemos = allMemos.Count;
        int totalMemoPages = Mathf.CeilToInt((float)totalMemos / MemosPerPage);

        if (totalMemos == 0)
        {
            _currentMemoPage = 0;
        }
        else if (_currentMemoPage >= totalMemoPages)
        {
            _currentMemoPage = totalMemoPages - 1;
        }
        else if (_currentMemoPage < 0)
        {
            _currentMemoPage = 0;
        }

        int startIndex = _currentMemoPage * MemosPerPage;
        List<string> displayedMemos = allMemos
            .Skip(startIndex)
            .Take(MemosPerPage)
            .ToList();

        if (displayedMemos.Count > 0)
        {
            for (int i = 0; i < displayedMemos.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.SelectableLabel(displayedMemos[i], EditorStyles.wordWrappedLabel);
                if (GUILayout.Button("삭제", GUILayout.Width(50)))
                {
                    int originalIndex = startIndex + i;
                    if (originalIndex < _currentEntry.Memos.Count)
                    {
                        _currentEntry.Memos.RemoveAt(originalIndex);
                        _parentHandler.UpdateQuestionEntry(_currentEntry);
                        // 메모 삭제 후, 현재 페이지의 메모가 모두 삭제되면 이전 페이지로 이동
                        if (displayedMemos.Count == 1 && _currentMemoPage > 0 && (_currentEntry.Memos.Count % MemosPerPage == 0))
                        {
                            _currentMemoPage--;
                        }
                        Repaint();
                    }
                    GUIUtility.ExitGUI();
                }
                EditorGUILayout.EndHorizontal();
            }
        }
        else
        {
            EditorGUILayout.HelpBox("추가된 메모가 없습니다.", MessageType.Info);
        }

        EditorGUILayout.EndScrollView();

        // 메모 페이징 UI
        if (totalMemoPages > 1)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUI.enabled = (_currentMemoPage > 0);
            if (GUILayout.Button("◀ 이전", GUILayout.Width(60)))
            {
                _currentMemoPage--;
                _memoListScrollPos.y = 0;
            }
            GUI.enabled = true;

            EditorGUILayout.LabelField($"{_currentMemoPage + 1} / {totalMemoPages}", EditorStyles.centeredGreyMiniLabel, GUILayout.Width(60));

            GUI.enabled = (_currentMemoPage < totalMemoPages - 1);
            if (GUILayout.Button("다음 ▶", GUILayout.Width(60)))
            {
                _currentMemoPage++;
                _memoListScrollPos.y = 0;
            }
            GUI.enabled = true;
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("새 메모 추가:", EditorStyles.boldLabel);
        _newMemoText = EditorGUILayout.TextArea(_newMemoText, GUILayout.MinHeight(50));

        if (GUILayout.Button("메모 추가", GUILayout.Height(30)))
        {
            if (!string.IsNullOrEmpty(_newMemoText.Trim()))
            {
                _parentHandler.AddMemoToQuestion(_currentEntry, _newMemoText.Trim());
                _newMemoText = "";
                int newTotalMemos = _currentEntry.Memos.Count;
                int newTotalMemoPages = Mathf.CeilToInt((float)newTotalMemos / MemosPerPage);
                // 새 메모 추가 후 마지막 페이지로 이동
                if (newTotalMemos > 0 && _currentMemoPage < newTotalMemoPages -1 ) 
                {
                    _currentMemoPage = newTotalMemoPages - 1;
                }
                Repaint();
            }
            else
            {
                EditorUtility.DisplayDialog("경고", "메모 내용을 입력해주세요.", "확인");
            }
        }
        EditorGUILayout.EndVertical();

        if (GUILayout.Button("닫기", GUILayout.Height(30)))
        {
            Close();
        }
    }
}