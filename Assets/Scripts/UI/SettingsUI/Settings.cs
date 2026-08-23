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

    private ScrollView conversationScroll;

    private VisualElement uiRoot;
    private VisualElement settingsHeader;
    private VisualElement settingsTabHeaderStrip;

    private const float MinVisibleWindowMargin = 60f;

    private bool isDraggingSettingsWindow;
    private Vector2 settingsDragStartPointer;
    private Vector2 settingsDragStartPosition;

    /*
     * Piper is the only provider we expose, so the voice list is flat:
     * a readable label for the dropdown paired with the backend voice id.
     * The ids must match the greeting clips in Assets/Audio/Greetings
     * and the voiceId entries on ConversationController.
     */
    private readonly List<VoiceOption> voiceOptions = new()
    {
        new VoiceOption("Alan (UK)", "en_GB-alan-medium"),
        new VoiceOption("Ryan (US)", "en_US-ryan-medium"),
        new VoiceOption("Joe (US)", "en_US-joe-medium"),
        new VoiceOption("Danny (US)", "en_US-danny-low"),
        new VoiceOption("Northern English Male (UK)", "en_GB-northern_english_male-medium"),
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

        uiRoot = root;

        // Configure optional settings window and wrench button (if present)
        settingsWindow = root.Q<VisualElement>("settings-window");
        settingsWrenchButton = root.Q<Button>("settings-wrench-button");
        if (settingsWrenchButton != null)
        {
            settingsWrenchButton.style.display = DisplayStyle.None;
            settingsWrenchButton.clicked += ToggleSettings;
        }

        settingsHeader = root.Q<VisualElement>("settings-header");

        conversationScroll = root.Q<ScrollView>("conversation-scroll");

        settingsTabHeaderStrip =
            root.Q<VisualElement>(
                className: "unity-tab-view__header-container"
            );

        if (settingsTabHeaderStrip == null)
        {
            Debug.LogWarning(
                "Could not find the TabView's header strip " +
                "(expected USS class 'unity-tab-view__header-container'). " +
                "Clicking the Settings/Conversation History tab labels " +
                "may start a window drag instead of switching tabs - " +
                "check this if tab switching stops working."
            );
        }

        if (settingsWindow != null)
        {
            settingsWindow.RegisterCallback<PointerDownEvent>(
                OnSettingsWindowPointerDown
            );

            settingsWindow.RegisterCallback<PointerMoveEvent>(
                OnSettingsWindowPointerMove
            );

            settingsWindow.RegisterCallback<PointerUpEvent>(
                OnSettingsWindowPointerUp
            );
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

        PopulateTTSVoices();

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

    private void PopulateTTSVoices()
    {
        if (voiceOptions.Count == 0)
        {
            ttsVoiceDropdown.choices =
                new List<string>();

            ttsVoiceDropdown.value =
                string.Empty;

            ttsVoiceDropdown.style.display =
                DisplayStyle.None;

            return;
        }

        List<string> labels =
            voiceOptions.ConvertAll(
                option => option.label
            );

        ttsVoiceDropdown.choices =
            labels;

        ttsVoiceDropdown.value =
            labels[0];

        ttsVoiceDropdown.style.display =
            DisplayStyle.Flex;
    }

    /// <summary>
    /// Maps the dropdown label back to the backend voice id.
    /// Returns an empty string when nothing matches.
    /// </summary>
    private string ResolveVoiceId(
        string label
    )
    {
        foreach (VoiceOption option in voiceOptions)
        {
            if (option.label == label)
            {
                return option.id;
            }
        }

        return string.Empty;
    }

    private async void HandleSaveSettings()
    {
        string selectedAgentType =
            agentTypeDropdown.value;


        // TTS model is fixed to Piper (only provider exposed)
        string selectedTTS = "Piper";

        // The dropdown shows labels; the backend wants the voice id.
        string selectedTTSVoice =
            ResolveVoiceId(ttsVoiceDropdown.value);

        string customPrompt =
            selectedAgentType == "Other"
                ? customPromptField.value.Trim()
                : string.Empty;


        if (
            string.IsNullOrWhiteSpace(
                selectedTTSVoice
            )
        )
        {
            Debug.LogError(
                $"Unknown voice selection: " +
                $"'{ttsVoiceDropdown.value}'."
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

        // Session is running - the wrench can reopen this window to
        // view Conversation History, but the settings themselves
        // (agent type, prompt, voice, Start button) are locked so
        // they can't be changed mid-session.
        agentTypeDropdown.SetEnabled(false);
        customPromptField.SetEnabled(false);
        ttsVoiceDropdown.SetEnabled(false);
        saveSettingsButton.SetEnabled(false);

        if (conversationController != null)
        {
            /*
             * The voice has to be set before StartAgent, otherwise
             * the startup sequence cannot pick the right greeting clip.
             */
            conversationController.SetVoice(
                settings.tts_voice
            );

            conversationController.ResetForNewSession();
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

    /// <summary>
    /// Appends a right-aligned chat bubble for something the user said.
    /// </summary>
    public void AddUserMessage(string text)
    {
        AddChatBubble(text, "chat-bubble--user", "chat-row--user");
    }

    /// <summary>
    /// Appends a left-aligned chat bubble for the agent's spoken reply.
    /// </summary>
    public void AddAgentMessage(string text)
    {
        AddChatBubble(text, "chat-bubble--agent", "chat-row--agent");
    }

    /// <summary>
    /// Appends a small centred system-style line for a file the agent
    /// just wrote, e.g. "Created PlayerMovement.cs" or
    /// "Modified PlayerMovement.cs".
    /// </summary>
    public void AddFileMessage(string fileName, bool isNew)
    {
        string verb = isNew ? "Created" : "Modified";

        AddChatBubble(
            $"{verb} {fileName}",
            "chat-bubble--file",
            "chat-row--file"
        );
    }

    private void AddChatBubble(string text, string bubbleClass, string rowClass)
    {
        if (conversationScroll == null || string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        VisualElement row = new VisualElement();
        row.AddToClassList("chat-row");
        row.AddToClassList(rowClass);

        Label bubble = new Label(text);
        bubble.AddToClassList("chat-bubble");
        bubble.AddToClassList(bubbleClass);

        row.Add(bubble);
        conversationScroll.Add(row);

        conversationScroll.schedule.Execute(
            () => conversationScroll.ScrollTo(row)
        );
    }

    // ----------------------------
    // SETTINGS WINDOW DRAGGING
    // ----------------------------

    /// <summary>
    /// Controls that need their own pointer input (dropdowns, the
    /// prompt text field, the Start button, conversation scrolling,
    /// and the tab-switching strip) are excluded from starting a
    /// window drag - everywhere else on the window (including the
    /// header and the empty space around fields) drags it, and this
    /// is checked at click-time rather than tied to whether the
    /// fields are currently enabled, so dragging keeps working after
    /// Start locks them.
    /// </summary>
    private bool IsExcludedFromWindowDrag(VisualElement target)
    {
        if (target == null)
        {
            return false;
        }

        return
            IsSameOrDescendant(agentTypeDropdown, target) ||
            IsSameOrDescendant(customPromptField, target) ||
            IsSameOrDescendant(ttsVoiceDropdown, target) ||
            IsSameOrDescendant(saveSettingsButton, target) ||
            IsSameOrDescendant(conversationScroll, target) ||
            IsSameOrDescendant(settingsTabHeaderStrip, target);
    }

    private static bool IsSameOrDescendant(
        VisualElement ancestor,
        VisualElement target
    )
    {
        return
            ancestor != null &&
            (ancestor == target || ancestor.Contains(target));
    }

    private void OnSettingsWindowPointerDown(PointerDownEvent evt)
    {
        if (evt.button != 0)
        {
            return;
        }

        if (IsExcludedFromWindowDrag(evt.target as VisualElement))
        {
            return;
        }

        isDraggingSettingsWindow = true;

        settingsDragStartPointer = evt.position;

        settingsDragStartPosition = new Vector2(
            settingsWindow.resolvedStyle.left,
            settingsWindow.resolvedStyle.top
        );

        settingsWindow.CapturePointer(evt.pointerId);

        evt.StopPropagation();
    }

    private void OnSettingsWindowPointerMove(PointerMoveEvent evt)
    {
        if (
            !isDraggingSettingsWindow ||
            !settingsWindow.HasPointerCapture(evt.pointerId)
        )
        {
            return;
        }

        Vector2 currentPointer = new Vector2(evt.position.x, evt.position.y);
        Vector2 delta = currentPointer - settingsDragStartPointer;

        Vector2 clamped = ClampWindowPosition(
            settingsWindow,
            settingsDragStartPosition.x + delta.x,
            settingsDragStartPosition.y + delta.y
        );

        settingsWindow.style.left = clamped.x;
        settingsWindow.style.top = clamped.y;

        evt.StopPropagation();
    }

    private void OnSettingsWindowPointerUp(PointerUpEvent evt)
    {
        if (!isDraggingSettingsWindow)
        {
            return;
        }

        isDraggingSettingsWindow = false;

        if (settingsWindow.HasPointerCapture(evt.pointerId))
        {
            settingsWindow.ReleasePointer(evt.pointerId);
        }

        evt.StopPropagation();
    }

    /// <summary>
    /// Keeps at least MinVisibleWindowMargin worth of the dragged
    /// window within the visible root area on every side, so its
    /// header can never end up fully off-screen and unreachable.
    /// </summary>
    private Vector2 ClampWindowPosition(
        VisualElement window,
        float proposedLeft,
        float proposedTop
    )
    {
        if (uiRoot == null)
        {
            return new Vector2(proposedLeft, proposedTop);
        }

        float rootWidth = uiRoot.resolvedStyle.width;
        float rootHeight = uiRoot.resolvedStyle.height;
        float windowWidth = window.resolvedStyle.width;
        float windowHeight = window.resolvedStyle.height;

        float minLeft = MinVisibleWindowMargin - windowWidth;
        float maxLeft = rootWidth - MinVisibleWindowMargin;

        float minTop = 0f;
        float maxTop = rootHeight - MinVisibleWindowMargin;

        float clampedLeft = Mathf.Clamp(
            proposedLeft,
            Mathf.Min(minLeft, maxLeft),
            Mathf.Max(minLeft, maxLeft)
        );

        float clampedTop = Mathf.Clamp(
            proposedTop,
            Mathf.Min(minTop, maxTop),
            Mathf.Max(minTop, maxTop)
        );

        return new Vector2(clampedLeft, clampedTop);
    }

    private void OnDisable()
    {
        if (settingsWindow != null)
        {
            settingsWindow.UnregisterCallback<PointerDownEvent>(
                OnSettingsWindowPointerDown
            );

            settingsWindow.UnregisterCallback<PointerMoveEvent>(
                OnSettingsWindowPointerMove
            );

            settingsWindow.UnregisterCallback<PointerUpEvent>(
                OnSettingsWindowPointerUp
            );
        }

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


public class VoiceOption
{
    public readonly string label;
    public readonly string id;

    public VoiceOption(
        string label,
        string id
    )
    {
        this.label = label;
        this.id = id;
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