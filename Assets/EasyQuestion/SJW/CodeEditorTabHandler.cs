// Editor/CodeEditorTabHandler.cs
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

    // ⭐ 1. GeminiChatGPTIntegrationEditor 인스턴스를 저장할 필드를 추가합니다.
    private GeminiChatGPTIntegrationEditor parentEditorWindow;

    // ⭐ 2. Initialize 메서드를 추가하여 부모 에디터 창 인스턴스를 받습니다.
    public void Initialize(GeminiChatGPTIntegrationEditor parentWindow)
    {
        parentEditorWindow = parentWindow;
        // OnEnable 시 프로젝트 뷰의 선택된 스크립트 자동 로드
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

        EditorGUILayout.BeginVertical(EditorStyles.helpBox); // 예쁜 섹션 박스 스타일
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
            if (GUILayout.Button("💾 수정 확인 및 히스토리 저장", GUILayout.Height(40)))
            {
                if (EditorUtility.DisplayDialog("코드 저장", $"'{selectedScript.name}.cs' 파일에 변경 사항을 저장하고 히스토리에 추가하시겠습니까?", "예", "아니오"))
                {
                    SaveCodeAndRecordHistory(selectedScript.name + ".cs", selectedScript.text, currentCodeContent);
                    AssetDatabase.Refresh();
                    EditorUtility.DisplayDialog("저장 완료", "코드가 성공적으로 저장되고 히스토리에 기록되었습니다.", "확인");
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

    private void SaveCodeAndRecordHistory(string fileName, string originalCode, string modifiedCode)
    {
        if (selectedScript == null || string.IsNullOrEmpty(currentModifiedScriptPath))
        {
            EditorUtility.DisplayDialog("오류", "수정할 스크립트가 선택되지 않았습니다.", "확인");
            return;
        }

        // 1. 현재 스크립트 파일에 수정된 내용 저장
        File.WriteAllText(currentModifiedScriptPath, modifiedCode, System.Text.Encoding.UTF8);

        // 2. 히스토리 저장 폴더 생성 및 이전 코드 저장 (선택 사항이지만 유용함)
        string scriptFolderPath = Path.GetDirectoryName(currentModifiedScriptPath);
        string oldScriptsFolderPath = Path.Combine(scriptFolderPath, "OldScripts");
        if (!Directory.Exists(oldScriptsFolderPath))
        {
            Directory.CreateDirectory(oldScriptsFolderPath);
        }

        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string historyFolderName = $"{Path.GetFileNameWithoutExtension(fileName)}_{timestamp}";
        string historyFolderPath = Path.Combine(oldScriptsFolderPath, historyFolderName);
        Directory.CreateDirectory(historyFolderPath);

        // 원본 코드를 별도 파일로 저장
        string originalCodeFilePath = Path.Combine(historyFolderPath, "original_code.txt");
        File.WriteAllText(originalCodeFilePath, originalCode, System.Text.Encoding.UTF8);

        // ⭐ 3. CodeHistoryViewerTabHandler 인스턴스를 통해 RecordCodeChange 호출
        if (parentEditorWindow != null)
        {
            CodeHistoryViewerTabHandler historyHandler = parentEditorWindow.GetCodeHistoryViewerTabHandler();
            if (historyHandler != null)
            {
                historyHandler.RecordCodeChange(fileName, timestamp, originalCode, modifiedCode, currentModifiedScriptPath);
            }
            else
            {
                Debug.LogError("CodeHistoryViewerTabHandler 인스턴스를 찾을 수 없습니다. GeminiChatGPTIntegrationEditor에서 올바르게 초기화되었는지 확인하세요.");
            }
        }
        else
        {
            Debug.LogError("부모 에디터 창 인스턴스가 null입니다. CodeEditorTabHandler.Initialize가 호출되었는지 확인하세요.");
        }

        // ⭐ UI 갱신 (선택 사항, 필요에 따라 추가)
        // EditorWindow.GetWindow<GeminiChatGPTIntegrationEditor>().Repaint(); // RecordCodeChange 내부에서 이미 호출될 가능성 있음.
    }
}