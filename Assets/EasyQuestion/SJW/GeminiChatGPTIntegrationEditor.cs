using UnityEditor;
using UnityEngine;
using System.IO;
using System.Linq;

public class GeminiChatGPTIntegrationEditor : EditorWindow
{
    private GeminiTabHandler _geminiTabHandler;
    private ChatGPTTabHandler _chatGPTTabHandler;
    private QuestionListTabHandler _questionListTabHandler;
    private StatisticsTabHandler _statisticsTabHandler;

    private int _selectedTabIndex = 0;
    private string[] _tabNames = { "Gemini AI", "ChatGPT AI", "질문 리스트", "통계" };

    private Texture2D _bannerTexture;

    [MenuItem("Tools/Easy Fast AI Question")]
    public static void ShowWindow()
    {
        GetWindow<GeminiChatGPTIntegrationEditor>("AI Code Generator");
    }

    private void OnEnable()
    {
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
            Debug.LogWarning($"배너 이미지(banner.png)를 다음 경로에서 찾을 수 없습니다: {bannerPath}");
            _bannerTexture = null;
        }

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

        if (_questionListTabHandler == null)
        {
            _questionListTabHandler = new QuestionListTabHandler();
        }
        _questionListTabHandler.Initialize(this);

        if (_statisticsTabHandler == null)
        {
            _statisticsTabHandler = new StatisticsTabHandler();
        }
        _statisticsTabHandler.Initialize(this);
    }
    
    public QuestionListTabHandler GetQuestionListTabHandler()
    {
        if (_questionListTabHandler == null)
        {
            _questionListTabHandler = new QuestionListTabHandler();
            _questionListTabHandler.Initialize(this);
        }
        return _questionListTabHandler;
    }

    public StatisticsTabHandler GetStatisticsTabHandler()
    {
        if (_statisticsTabHandler == null)
        {
            _statisticsTabHandler = new StatisticsTabHandler();
            _statisticsTabHandler.Initialize(this);
        }
        return _statisticsTabHandler;
    }

    public GeminiTabHandler GetGeminiTabHandler()
    {
        if (_geminiTabHandler == null)
            _geminiTabHandler = new GeminiTabHandler();
        return _geminiTabHandler;
    }

    public ChatGPTTabHandler GetChatGPTTabHandler()
    {
        if (_chatGPTTabHandler == null)
            _chatGPTTabHandler = new ChatGPTTabHandler();
        return _chatGPTTabHandler;
    }

    private void OnGUI()
    {
        if (_bannerTexture != null)
        {
            Rect bannerRect = new Rect(0, 0, position.width, 100);
            GUI.DrawTexture(bannerRect, _bannerTexture, ScaleMode.ScaleToFit);
            EditorGUILayout.Space(105);
        }
        else
        {
            EditorGUILayout.Space(10);
        }

        int prevSelectedTabIndex = _selectedTabIndex;
        _selectedTabIndex = GUILayout.Toolbar(_selectedTabIndex, _tabNames);

        if (prevSelectedTabIndex != _selectedTabIndex)
        {
            Debug.Log($"탭 활성화: {_tabNames[_selectedTabIndex]}");
            if (_selectedTabIndex == 2) // 질문 리스트 탭으로 이동 시
            {
                _questionListTabHandler?.LoadHistory();
            }
            if (_selectedTabIndex == 3) // 통계 탭으로 이동 시
            {
                _statisticsTabHandler?.LoadKeywordsAndRefresh();
            }
        }

        EditorGUILayout.Space(10);

        float editorWindowWidth = position.width;
        float editorWindowHeight = position.height;

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
            case 3: // 통계 탭
                _statisticsTabHandler?.OnGUI(editorWindowWidth, editorWindowHeight);
                break;
        }
    }
}