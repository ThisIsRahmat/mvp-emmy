using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UIElements;

public class EmmyUI : MonoBehaviour
{
    [SerializeField]
    private VisualTreeAsset m_VisualTreeAsset = default;

    private DropdownField agentTypeDropdown;
    private DropdownField LLMDropdown;
    private DropdownField TTSDropdown;
    private DropdownField ttsVoiceDropdown;

    private TextField customPromptField;

    private Button saveSettingsButton;

    private UIDocument uiDocument;


    private readonly Dictionary<string, List<string>> ttsVoiceOptions = new()
    {
        {
            "Kokoro",
            new List<string>
            {
                "am_adam",
                "am_michael",
                "am_liam",
                "bm_george",
                "bm_daniel"
            }
        },
        {
            "Piper",
            new List<string>
            {
                "en_US-ryan-high",
                "en_GB-alan-medium"
            }
        }
    };

    [MenuItem("Window/UI Toolkit/Emmy")]
    public static void ShowExample()
    {
        Emmy window = GetWindow<Emmy>();
        window.titleContent = new GUIContent("Emmy");
    }

    public void CreateGUI()
    {
        if (m_VisualTreeAsset == null)
        {
            Debug.LogError(
                "Emmy: No VisualTreeAsset has been assigned."
            );

            return;
        }

        VisualElement ui =
            m_VisualTreeAsset.Instantiate();

        rootVisualElement.Add(ui);

        conversationScroll = rootVisualElement.Q<ScrollView>("conversation-scroll");


        // Find controls from UXML
        agentTypeDropdown =
            rootVisualElement.Q<DropdownField>(
                "agent-type-dropdown"
            );

        LLMDropdown =
            rootVisualElement.Q<DropdownField>(
                "llm-dropdown"
            );

        TTSDropdown =
            rootVisualElement.Q<DropdownField>(
                "tts-dropdown"
            );

        ttsVoiceDropdown =
            rootVisualElement.Q<DropdownField>(
                "tts-voice-dropdown"
            );


        customPromptField =
            rootVisualElement.Q<TextField>(
                "custom-prompt-field"
            );

        saveSettingsButton =
            rootVisualElement.Q<Button>(
                "save-settings-button"
            );

        // Validate
        if (
            agentTypeDropdown == null ||
            LLMDropdown == null ||
            TTSDropdown == null ||
            ttsVoiceDropdown == null ||
            customPromptField == null ||
            saveSettingsButton == null
        )
        {
            Debug.LogError(
                "One or more Emmy UI controls could not be found. " +
                "Check the names in Emmy.uxml."
            );

            return;
        }

        ConfigureAgentTypeDropdown();
        ConfigureLLMDropdown();
        ConfigureTTSDropdown();


        saveSettingsButton.clicked += HandleSaveSettings;
    }

    private void ConfigureAgentTypeDropdown()
    {
        agentTypeDropdown.choices =
            new List<string>
            {
                "Instructor",
                "Peer",
                "Other"
            };

        agentTypeDropdown.value = "Peer";

        customPromptField.value = string.Empty;

        customPromptField.style.display =
            DisplayStyle.None;

        agentTypeDropdown.RegisterValueChangedCallback(
            HandleAgentTypeChanged
        );
    }

    private void ConfigureLLMDropdown()
    {
        LLMDropdown.choices =
            new List<string>
            {
                "Qwen3",
                "Gemma 3",
                "Devstral"
            };

        LLMDropdown.value = "Qwen3";
    }

    private void ConfigureTTSDropdown()
    {
        TTSDropdown.choices =
            new List<string>
            {
                "Kokoro",
                "Piper"
            };

        ttsVoiceDropdown.style.display =
            DisplayStyle.None;

        TTSDropdown.RegisterValueChangedCallback(
            HandleTTSChanged
        );

        TTSDropdown.value = "Kokoro";

        PopulateTTSVoices("Kokoro");
    }



    private void HandleAgentTypeChanged(
        ChangeEvent<string> changeEvent
    )
    {
        bool usesCustomPrompt =
            changeEvent.newValue == "Other";

        customPromptField.style.display =
            usesCustomPrompt
                ? DisplayStyle.Flex
                : DisplayStyle.None;

        if (!usesCustomPrompt)
        {
            customPromptField.value =
                string.Empty;
        }
    }

    private void HandleTTSChanged(
        ChangeEvent<string> changeEvent
    )
    {
        PopulateTTSVoices(
            changeEvent.newValue
        );
    }

    private void PopulateTTSVoices(
        string selectedTTS
    )
    {
        if (
            !ttsVoiceOptions.TryGetValue(
                selectedTTS,
                out List<string> voices
            ) ||
            voices.Count == 0
        )
        {
            ttsVoiceDropdown.choices =
                new List<string>();

            ttsVoiceDropdown.value =
                string.Empty;

            ttsVoiceDropdown.style.display =
                DisplayStyle.None;

            return;
        }

        ttsVoiceDropdown.choices = voices;

        ttsVoiceDropdown.value =
            voices[0];

        ttsVoiceDropdown.style.display =
            DisplayStyle.Flex;
    }

    private async void HandleSaveSettings()
    {
        string selectedAgentType =
            agentTypeDropdown.value;

        string selectedLLM =
            LLMDropdown.value;

        string selectedTTS =
            TTSDropdown.value;

        string selectedTTSVoice =
            ttsVoiceDropdown.value;


        string customPrompt =
            selectedAgentType == "Other"
                ? customPromptField.value.Trim()
                : string.Empty;

        if (string.IsNullOrWhiteSpace(selectedLLM))
        {
            Debug.LogError("Select an LLM.");
            return;
        }

        if (string.IsNullOrWhiteSpace(selectedTTS))
        {
            Debug.LogError("Select a TTS model.");
            return;
        }

        if (string.IsNullOrWhiteSpace(selectedTTSVoice))
        {
            Debug.LogError(
                "Select a voice for the selected TTS model."
            );

            return;
        }


        if (
            selectedAgentType == "Other" &&
            string.IsNullOrWhiteSpace(customPrompt)
        )
        {
            Debug.LogError(
                "Enter a custom prompt when Agent Type is Other."
            );

            return;
        }

        AgentSettings settings =
            new AgentSettings
            {
                agent_type = selectedAgentType,
                llm = selectedLLM,
                tts = selectedTTS,
                tts_voice = selectedTTSVoice,
                custom_prompt = customPrompt
            };

        await SendSettings(settings);
    }

    private async Task SendSettings(
        AgentSettings settings
    )
    {
        string json =
            JsonUtility.ToJson(settings);

        byte[] bodyRaw =
            System.Text.Encoding.UTF8.GetBytes(json);

        using UnityWebRequest request =
            new UnityWebRequest(
                "http://127.0.0.1:8000/settings",
                "POST"
            );

        request.uploadHandler =
            new UploadHandlerRaw(bodyRaw);

        request.downloadHandler =
            new DownloadHandlerBuffer();

        request.SetRequestHeader(
            "Content-Type",
            "application/json"
        );

        saveSettingsButton.SetEnabled(false);
        saveSettingsButton.text = "Starting...";

        UnityWebRequestAsyncOperation operation =
            request.SendWebRequest();

        while (!operation.isDone)
        {
            await Task.Yield();
        }

        if (
            request.result !=
            UnityWebRequest.Result.Success
        )
        {
            Debug.LogError(
                $"Could not save settings: " +
                $"{request.error}\n" +
                $"{request.downloadHandler.text}"
            );

            saveSettingsButton.SetEnabled(true);
            saveSettingsButton.text = "Start";

            return;
        }

        Debug.Log(
            $"Agent started successfully.\n" +
            $"LLM: {settings.llm}\n" +
            $"TTS: {settings.tts} " +
            $"({settings.tts_voice})\n" +
            $"Agent Type: {settings.agent_type}"
        );

        saveSettingsButton.text = "Started";

        Debug.Log("Settings saved. Starting Emmy...");

        if (!EditorApplication.isPlaying)
        {
            EditorApplication.isPlaying = true;
        }
    }

    private void OnDisable()
    {
        if (saveSettingsButton != null)
        {
            saveSettingsButton.clicked -=
                HandleSaveSettings;
        }
    }
}

[System.Serializable]
public class AgentSettings
{
    public string agent_type;
    public string llm;
    public string tts;
    public string tts_voice;
    public string custom_prompt;
}
