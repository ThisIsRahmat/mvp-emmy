using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UIElements;

public class Settings : MonoBehaviour
{
    private DropdownField agentTypeDropdown;
    // TTS model selection removed; we default to a single TTS provider and expose only voices
    private DropdownField ttsVoiceDropdown;

    private TextField customPromptField;
    private Button saveSettingsButton;

    private UIDocument uiDocument;

    private VisualElement settingsWindow;
    private Button settingsWrenchButton;
    [SerializeField]
    private ConversationController conversationController;

    private readonly Dictionary<string, List<string>>
        ttsVoiceOptions = new()
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

    private void OnEnable()
    {
        uiDocument = GetComponent<UIDocument>();

        if (uiDocument == null)
        {
            Debug.LogError(
                "SettingsUI requires a UIDocument component."
            );

            return;
        }

        VisualElement root =
            uiDocument.rootVisualElement;

        // Configure optional settings window and wrench button (if present)
        settingsWindow = root.Q<VisualElement>("settings-window");
        settingsWrenchButton = root.Q<Button>("settings-wrench-button");
        if (settingsWrenchButton != null)
        {
            settingsWrenchButton.style.display = DisplayStyle.None;
            settingsWrenchButton.clicked += ToggleSettings;

            settingsWindow = root.Q<VisualElement>("settings-window");
settingsWrenchButton = root.Q<Button>("settings-wrench-button");

if (settingsWrenchButton != null)
{
    settingsWrenchButton.style.display = DisplayStyle.None;
    settingsWrenchButton.clicked += ToggleSettings;
}

            

        }

        // Find controls from UXML
        agentTypeDropdown =
            root.Q<DropdownField>(
                "agent-type-dropdown"
            );

        ttsVoiceDropdown =
            root.Q<DropdownField>(
                "tts-voice-dropdown"
            );

        // Populate voices for the single supported TTS provider (default: Kokoro)
        PopulateTTSVoices("Kokoro");

        customPromptField =
            root.Q<TextField>(
                "custom-prompt-field"
            );

        saveSettingsButton =
            root.Q<Button>(
                "save-settings-button"
            );

        if (
            agentTypeDropdown == null ||
            ttsVoiceDropdown == null ||
            customPromptField == null ||
            saveSettingsButton == null
        )
        {
            Debug.LogError(
                "One or more Emmy UI controls could not be found. " +
                "Check the element names in Emmy.uxml."
            );

            return;
        }

        ConfigureAgentTypeDropdown();

        saveSettingsButton.clicked +=
            HandleSaveSettings;
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

        customPromptField.value =
            string.Empty;

        customPromptField.style.display =
            DisplayStyle.None;

        agentTypeDropdown.RegisterValueChangedCallback(
            HandleAgentTypeChanged
        );
    }


    // TTS model selection removed: voices are populated directly for the single provider.
    // No TTSDropdown to unregister (single TTS provider)

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

    // TTS model selection removed: voices are populated directly for the single provider.

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

        ttsVoiceDropdown.choices =
            voices;

        ttsVoiceDropdown.value =
            voices[0];

        ttsVoiceDropdown.style.display =
            DisplayStyle.Flex;
    }

    private async void HandleSaveSettings()
    {
        string selectedAgentType =
            agentTypeDropdown.value;


        // TTS model is fixed to Kokoro (only provider exposed)
        string selectedTTS = "Kokoro";

        string selectedTTSVoice =
            ttsVoiceDropdown.value;

        string customPrompt =
            selectedAgentType == "Other"
                ? customPromptField.value.Trim()
                : string.Empty;


        if (string.IsNullOrWhiteSpace(selectedTTS))
        {
            Debug.LogError(
                "Select a TTS model."
            );

            return;
        }

        if (
            string.IsNullOrWhiteSpace(
                selectedTTSVoice
            )
        )
        {
            Debug.LogError(
                "Select a voice for the selected TTS model."
            );

            return;
        }

        if (
            selectedAgentType == "Other" &&
            string.IsNullOrWhiteSpace(
                customPrompt
            )
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
            System.Text.Encoding.UTF8.GetBytes(
                json
            );

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

        saveSettingsButton.text =
            "Starting...";

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

            saveSettingsButton.SetEnabled(
                true
            );

            saveSettingsButton.text =
                "Start";

            return;
        }

        Debug.Log(
            $"Agent started successfully.\n" +
            $"TTS: {settings.tts} " +
            $"({settings.tts_voice})\n" +
            $"Agent Type: " +
            $"{settings.agent_type}"
        );

        saveSettingsButton.text =
            "Started";

                Debug.Log(
            "Settings saved. Starting Emmy..."
        );

        settingsWindow.style.display =
            DisplayStyle.None;

        settingsWrenchButton.style.display =
            DisplayStyle.Flex;

        if (conversationController != null)
        {
            conversationController.StartAgent();
        }
        else
        {
            Debug.LogError("ConversationController is not assigned.");
        }
    }

    private void ToggleSettings()
{
    bool isVisible =
        settingsWindow.style.display !=
        DisplayStyle.None;

    settingsWindow.style.display =
        isVisible
            ? DisplayStyle.None
            : DisplayStyle.Flex;
}

    private void OnDisable()
    {
        if (saveSettingsButton != null)
        {
            saveSettingsButton.clicked -=
                HandleSaveSettings;
        }

        if (agentTypeDropdown != null)
        {
            agentTypeDropdown
                .UnregisterValueChangedCallback(
                    HandleAgentTypeChanged
                );
        }

        // No TTSDropdown to unregister (single TTS provider)
    }
}


[System.Serializable]
public class AgentSettings
{
    public string agent_type;
    public string tts;
    public string tts_voice;
    public string custom_prompt;
}