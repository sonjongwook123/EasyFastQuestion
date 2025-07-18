using UnityEditor;
using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System;

public class CodeEditorTabHandler
{
    private string searchFileName = "";
    private MonoScript selectedScript;
    private string currentCodeContent = "";
    private Vector2 codeScrollPos;
    private string currentModifiedScriptPath;

    public void Initialize(GeminiChatGPTIntegrationEditor parentWindow)
    {
        if (Selection.activeObject != null && Selection.activeObject is MonoScript)
        {
            selectedScript = Selection.activeObject as MonoScript;
            currentCodeContent = selectedScript.text;
            currentModifiedScriptPath = AssetDatabase.GetAssetPath(selectedScript);
        }
    }

    public void OnGUI()
    {
        EditorGUILayout.LabelField("✍️ 코드 수정", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("📂 스크립트 선택/검색", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        searchFileName = EditorGUILayout.TextField("파일 이름 검색:", searchFileName);
        if (GUILayout.Button("🔍 검색", GUILayout.Width(60), GUILayout.Height(25)))
        {
            PerformScriptSearch(searchFileName);
        }
        EditorGUILayout.EndHorizontal();

        if (selectedScript != null)
        {
            EditorGUILayout.HelpBox($"선택된 스크립트: {selectedScript.name}.cs", MessageType.Info);
            EditorGUILayout.LabelField("📝 스크립트 내용:", EditorStyles.boldLabel);
            codeScrollPos = EditorGUILayout.BeginScrollView(codeScrollPos, GUILayout.ExpandHeight(true));
            currentCodeContent = EditorGUILayout.TextArea(currentCodeContent, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(10);
            if (GUILayout.Button("💾 수정 확인 및 저장", GUILayout.Height(40)))
            {
                if (EditorUtility.DisplayDialog("코드 저장", $"'{selectedScript.name}.cs' 파일에 변경 사항을 저장하시겠습니까?", "예", "아니오"))
                {
                    SaveCode(selectedScript.name + ".cs", currentCodeContent);
                    AssetDatabase.Refresh();
                    EditorUtility.DisplayDialog("저장 완료", "코드가 성공적으로 저장되었습니다.", "확인");
                }
            }
        }
        else
        {
            EditorGUILayout.HelpBox("수정할 스크립트를 검색하거나 프로젝트 뷰에서 선택해주세요.", MessageType.Info);
        }
        EditorGUILayout.EndVertical();
    }

    private void PerformScriptSearch(string fileName)
    {
        string[] guids = AssetDatabase.FindAssets($"{fileName} t:Script");
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            selectedScript = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
            if (selectedScript != null)
            {
                currentCodeContent = selectedScript.text;
                currentModifiedScriptPath = path;
            }
        }
        else
        {
            selectedScript = null;
            currentCodeContent = "";
            currentModifiedScriptPath = "";
            EditorUtility.DisplayDialog("알림", "해당 이름의 스크립트를 찾을 수 없습니다.", "확인");
        }
    }

    private void SaveCode(string fileName, string modifiedCode)
    {
        if (selectedScript == null || string.IsNullOrEmpty(currentModifiedScriptPath))
        {
            EditorUtility.DisplayDialog("오류", "수정할 스크립트가 선택되지 않았습니다.", "확인");
            return;
        }

        File.WriteAllText(currentModifiedScriptPath, modifiedCode, System.Text.Encoding.UTF8);
    }
}