﻿using UnityEditor;
using UnityEngine;

public class EditorInputDialog : EditorWindow
{
    private static string _inputText;
    private static string _dialogTitle;
    private static string _message;
    private static bool _didConfirm;
    private static EditorInputDialog _window;

    public static string Show(string title, string message, string defaultInput = "")
    {
        _dialogTitle = title;
        _message = message;
        _inputText = defaultInput;
        _didConfirm = false;

        _window = GetWindow<EditorInputDialog>(true, _dialogTitle, true);
        _window.minSize = new Vector2(300, 100);
        _window.maxSize = new Vector2(300, 100);
        _window.ShowModalUtility();
        
        while (_window != null && _window.minSize.x != -1)
        {
            System.Threading.Thread.Sleep(50);
            _window.Repaint();
        }
        
        return _didConfirm ? _inputText : null;
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField(_message, EditorStyles.wordWrappedLabel);
        _inputText = EditorGUILayout.TextField(_inputText);

        EditorGUILayout.Space();

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("확인", GUILayout.Width(80)))
        {
            _didConfirm = true;
            Close();
        }
        if (GUILayout.Button("취소", GUILayout.Width(80)))
        {
            _didConfirm = false;
            Close();
        }
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
    }

    private void OnLostFocus()
    {
        // 모달 창이므로 포커스를 잃어도 자동으로 닫히지 않도록 합니다.
    }

    private void OnDestroy()
    {
        if (_window != null)
        {
            _window.minSize = new Vector2(-1, -1);
        }
    }
}