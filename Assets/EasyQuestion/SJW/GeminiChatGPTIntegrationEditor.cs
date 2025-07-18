// Editor/GeminiChatGPTIntegrationEditor.cs

using UnityEditor;
using UnityEngine;
using System.IO;

public class GeminiChatGPTIntegrationEditor : EditorWindow
{
    private GeminiTabHandler _geminiTabHandler;
    private ChatGPTTabHandler _chatGPTTabHandler;
    private CodeHistoryViewerTabHandler _codeHistoryViewerTabHandler;
    private QuestionListTabHandler _questionListTabHandler; 

    private int _selectedTabIndex = 0;
    private string[] _tabNames = { "Gemini AI", "ChatGPT AI", "질문 리스트", "코드 변경 내역" };

    private Texture2D _bannerTexture;

    [MenuItem("Window/Easy Question/AI Code Generator")]
    public static void ShowWindow()
    {
        GetWindow<GeminiChatGPTIntegrationEditor>("AI Code Generator");
    }

    private void OnEnable()
    {
        // 스크립트 파일이 있는 폴더에서 banner.png 찾기
        string scriptPath = AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(this));
        string scriptDirectory = Path.GetDirectoryName(scriptPath);
        string bannerPath = Path.Combine(scriptDirectory, "banner.png");

        if (File.Exists(bannerPath))
        {
            byte[] fileData = File.ReadAllBytes(bannerPath);
            _bannerTexture = new Texture2D(2, 2);
            _bannerTexture.LoadImage(fileData);
        }
        else
        {
            // 배너 이미지를 찾지 못했을 경우 경고 메시지 출력
            Debug.LogWarning($"배너 이미지(banner.png)를 다음 경로에서 찾을 수 없습니다: {bannerPath}");
            _bannerTexture = null;
        }

        // 각 탭 핸들러 초기화
        if (_geminiTabHandler == null)
        {
            _geminiTabHandler = new GeminiTabHandler();
        }
        _geminiTabHandler.Initialize(this);

        if (_chatGPTTabHandler == null)
        {
            _chatGPTTabHandler = new ChatGPTTabHandler();
        }
        _chatGPTTabHandler.Initialize(this);

        if (_codeHistoryViewerTabHandler == null)
        {
            _codeHistoryViewerTabHandler = new CodeHistoryViewerTabHandler();
        }
        _codeHistoryViewerTabHandler.Initialize(this);

        if (_questionListTabHandler == null)
        {
            _questionListTabHandler = new QuestionListTabHandler();
        }
        _questionListTabHandler.Initialize(this);
    }
    
    // CodeHistoryViewerTabHandler 인스턴스를 반환하는 메서드
    public CodeHistoryViewerTabHandler GetCodeHistoryViewerTabHandler()
    {
        if (_codeHistoryViewerTabHandler == null)
        {
            _codeHistoryViewerTabHandler = new CodeHistoryViewerTabHandler();
            _codeHistoryViewerTabHandler.Initialize(this);
        }
        return _codeHistoryViewerTabHandler;
    }

    // QuestionListTabHandler 인스턴스를 반환하는 메서드
    public QuestionListTabHandler GetQuestionListTabHandler()
    {
        if (_questionListTabHandler == null)
        {
            _questionListTabHandler = new QuestionListTabHandler();
            _questionListTabHandler.Initialize(this);
        }
        return _questionListTabHandler;
    }

    private void OnGUI()
    {
        // 이미지 배너 표시 (텍스트 라벨 삭제)
        if (_bannerTexture != null)
        {
            Rect bannerRect = new Rect(0, 0, position.width, 100); // 배너 높이 조정 가능
            GUI.DrawTexture(bannerRect, _bannerTexture, ScaleMode.ScaleToFit);
            EditorGUILayout.Space(105); // 배너 높이에 맞춰 공간 확보
        }
        // 배너 이미지가 없는 경우, 공간을 비워둠 (아무것도 그리지 않음)
        else
        {
            EditorGUILayout.Space(10); // 배너가 없을 때 기본적인 상단 여백
        }

        int prevSelectedTabIndex = _selectedTabIndex;
        _selectedTabIndex = GUILayout.Toolbar(_selectedTabIndex, _tabNames);

        // 탭이 변경되었을 때 콘솔에 메시지 출력
        if (prevSelectedTabIndex != _selectedTabIndex)
        {
            Debug.Log($"탭 활성화: {_tabNames[_selectedTabIndex]}");
        }

        EditorGUILayout.Space(10); // 탭 바 아래 여백

        float editorWindowWidth = position.width;
        float editorWindowHeight = position.height;

        // 선택된 탭에 따라 해당 핸들러의 OnGUI 호출
        switch (_selectedTabIndex)
        {
            case 0: // Gemini AI 탭
                _geminiTabHandler?.OnGUI(editorWindowWidth, editorWindowHeight);
                break;
            case 1: // ChatGPT AI 탭
                _chatGPTTabHandler?.OnGUI(editorWindowWidth, editorWindowHeight);
                break;
            case 2: // 질문 리스트 탭
                _questionListTabHandler?.OnGUI(editorWindowWidth, editorWindowHeight);
                break;
            case 3: // 코드 변경 내역 탭
                _codeHistoryViewerTabHandler?.OnGUI(editorWindowWidth, editorWindowHeight);
                break;
        }
    }
}