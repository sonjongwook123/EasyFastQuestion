using UnityEditor;
using UnityEngine;
using System.IO;

public class GeminiChatGPTIntegrationEditor : EditorWindow
{
    private int _selectedTab = 0;
    private string[] _tabNames = { "Gemini", "ChatGPT", "질문 리스트", "통계 분석" };

    private GeminiTabHandler _geminiTabHandler;
    private ChatGPTTabHandler _chatGPTTabHandler;
    private QuestionListTabHandler _questionListTabHandler;
    private StatisticsTabHandler _statisticsTabHandler;

    private Texture2D _bannerImage;

    [MenuItem("Tools/Gemini & ChatGPT Integration")]
    public static void ShowWindow()
    {
        GetWindow<GeminiChatGPTIntegrationEditor>("AI 통합 도구");
    }

    private void OnEnable()
    {
        _geminiTabHandler = new GeminiTabHandler();
        _geminiTabHandler.Initialize(this);

        _chatGPTTabHandler = new ChatGPTTabHandler();
        _chatGPTTabHandler.Initialize(this);

        _questionListTabHandler = new QuestionListTabHandler();
        _questionListTabHandler.Initialize(this);

        _statisticsTabHandler = new StatisticsTabHandler();
        _statisticsTabHandler.Initialize(this);

        LoadBannerImage();
    }

    private void LoadBannerImage()
    {
        string[] guids = AssetDatabase.FindAssets("t:Script " + typeof(GeminiChatGPTIntegrationEditor).Name);
        if (guids.Length > 0)
        {
            string scriptPath = AssetDatabase.GUIDToAssetPath(guids[0]);
            string scriptFolderPath = Path.GetDirectoryName(scriptPath);
            string bannerPath = Path.Combine(scriptFolderPath, "ai_banner.png");
            if (File.Exists(bannerPath))
            {
                byte[] fileData = File.ReadAllBytes(bannerPath);
                _bannerImage = new Texture2D(2, 2);
                _bannerImage.LoadImage(fileData);
            }
            else
            {
                Debug.LogWarning("ai_banner.png 이미지를 찾을 수 없습니다: " + bannerPath);
            }
        }
        else
        {
            Debug.LogWarning("GeminiChatGPTIntegrationEditor.cs 스크립트를 찾을 수 없습니다. 배너 이미지를 로드할 수 없습니다.");
        }
    }

    void OnGUI()
    {
        float bannerWidth = position.width;
        float bannerHeight = 100; 

        if (_bannerImage != null)
        {
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label(_bannerImage, GUILayout.Width(bannerWidth), GUILayout.Height(bannerHeight));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            EditorGUILayout.Space(10);

            GUILayout.MinHeight(800);
        }

        int newSelectedTab = GUILayout.Toolbar(_selectedTab, _tabNames);
        if (newSelectedTab != _selectedTab)
        {
            _selectedTab = newSelectedTab;
            Repaint();
        }

        EditorGUILayout.Space(10);

        switch (_selectedTab)
        {
            case 0:
                _geminiTabHandler.OnGUI(position.width, position.height);
                break;
            case 1:
                _chatGPTTabHandler.OnGUI(position.width, position.height);
                break;
            case 2:
                _questionListTabHandler.OnGUI(position.width, position.height);
                break;
            case 3:
                _statisticsTabHandler.OnGUI(position.width, position.height);
                break;
        }
    }

    public QuestionListTabHandler GetQuestionListTabHandler()
    {
        return _questionListTabHandler;
    }

    public StatisticsTabHandler GetStatisticsTabHandler()
    {
        return _statisticsTabHandler;
    }

    // New Public Getters for AI Handlers
    public GeminiTabHandler GetGeminiTabHandler()
    {
        return _geminiTabHandler;
    }

    public ChatGPTTabHandler GetChatGPTTabHandler()
    {
        return _chatGPTTabHandler;
    }
}