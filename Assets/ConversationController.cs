using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Networking;

[System.Serializable]
public class VoiceGreeting
{
    public string voiceId;
    public AudioClip greetingClip;
    public string greetingText;
}

public class ConversationController : MonoBehaviour
{

    [SerializeField]
    private Files files;

    [Header("Backend")]
    [SerializeField]
    private string backendBaseUrl = "http://127.0.0.1:8000";

    [SerializeField]
    private string agentEndpoint = "/agent/respond";

    [SerializeField]
    private string uploadFieldName = "audio";

    [Header("Greeting")]
    [SerializeField]
    private VoiceGreeting[] voiceGreetings;

    /*
     * Optional pause between the wave and the greeting audio.
     * Zero means they start together, which is what we want by default.
     */
    [SerializeField]
    private float waveHoldSeconds = 0f;

    /*
     * Greetings load from Resources by naming convention, so there is
     * nothing to wire in the Inspector: Assets/Resources/Greetings/
     * greeting_<voiceId>.wav. The voiceGreetings array above is only
     * needed to override a clip or attach on-screen text.
     */
    private const string GreetingResourcePrefix =
        "Greetings/greeting_";

    private string selectedVoiceId;

    [Header("Microphone")]
    [SerializeField]
    private MicRecorder micRecorder;

    [Header("Audio")]
    [SerializeField]
    private AudioSource speechAudioSource;

    [Header("Embodiment")]
    [SerializeField]
    private Animator characterAnimator;

    [Header("UI")]
    [SerializeField]
    private TMP_Text statusText;

    [SerializeField]
    private TMP_Text transcriptionText;

    [SerializeField]
    private TMP_Text responseText;

    [SerializeField]
    private TMP_Text codeText;

    [Header("Input")]
    [SerializeField]
    private bool useSpaceToTalk = true;

    private bool isBusy;
    private bool isRecording;
    private bool hasStarted;

    [Header("Filler Lines")]
    [SerializeField]
    private float fillerInitialDelayMinSeconds = 3f;

    [SerializeField]
    private float fillerInitialDelayMaxSeconds = 5f;

    [SerializeField]
    private float fillerMinGapSeconds = 15f;

    [SerializeField]
    private float fillerMaxGapSeconds = 20f;

    private Coroutine activeConversationCoroutine;
    private Coroutine fillerCoroutine;

    private void Awake()
    {
        // No VSync and no frame cap meant this rendered uncapped
        // (100s of FPS for a simple scene), pegging the CPU/GPU
        // continuously - this was starving the backend's STT/LLM/TTS
        // processes of resources and contributing to swap thrashing.
        Application.targetFrameRate = 60;

        ValidateReferences();
    }

    public void SetVoice(
        string voiceId
    )
    {
        selectedVoiceId = voiceId;

        Debug.Log(
            $"Greeting voice set to: {voiceId}"
        );
    }

    /// <summary>
    /// Returns the Inspector override for the selected voice, if one
    /// was assigned. Null is normal and means "use Resources".
    /// </summary>
    private VoiceGreeting FindGreetingOverride()
    {
        if (
            voiceGreetings == null ||
            string.IsNullOrWhiteSpace(selectedVoiceId)
        )
        {
            return null;
        }

        foreach (
            VoiceGreeting greeting
            in voiceGreetings
        )
        {
            if (
                greeting != null &&
                greeting.voiceId == selectedVoiceId
            )
            {
                return greeting;
            }
        }

        return null;
    }

    /// <summary>
    /// Loads Assets/Resources/Greetings/greeting_&lt;voiceId&gt;.wav.
    /// </summary>
    private AudioClip LoadGreetingFromResources()
    {
        if (
            string.IsNullOrWhiteSpace(
                selectedVoiceId
            )
        )
        {
            Debug.LogError(
                "No voice was selected, so no greeting can be loaded. " +
                "SetVoice must be called before StartAgent."
            );

            return null;
        }

        string resourcePath =
            GreetingResourcePrefix + selectedVoiceId;

        AudioClip clip =
            Resources.Load<AudioClip>(resourcePath);

        if (clip == null)
        {
            Debug.LogError(
                $"No greeting clip at Resources/{resourcePath}. " +
                $"Expected Assets/Resources/{resourcePath}.wav"
            );
        }

        return clip;
    }

    /// <summary>
    /// Called every time Settings is saved successfully - not just
    /// the first time, since Settings can be reopened later in the
    /// same run. The backend starts a fresh session/scratch folder
    /// on every /settings save, so the Files panel must match.
    /// </summary>
    public void ResetForNewSession()
    {
        files?.ClearSession();
    }

    public void StartAgent()
    {
        if (hasStarted)
        {
            return;
        }

        hasStarted = true;

        StartCoroutine(
            PlayStartupSequence()
        );
    }

    private void Update()
    {
        if (!useSpaceToTalk)
        {
            return;
        }

        if (Keyboard.current == null)
        {
            return;
        }

        /*
         * We allow the user to release Space while recording.
         * Input is blocked while the agent is processing or speaking.
         */
        if (!isBusy &&
            !isRecording &&
            Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            StartListening();
        }

        if (isRecording &&
            Keyboard.current.spaceKey.wasReleasedThisFrame)
        {
            StopListeningAndProcess();
        }
    }

    private IEnumerator PlayStartupSequence()
    {
        isBusy = true;
        isRecording = false;

        SetState(AgentState.Startup);

        if (characterAnimator != null)
        {
            characterAnimator.ResetTrigger("Wave");
            characterAnimator.SetTrigger("Wave");
        }

        if (waveHoldSeconds > 0f)
        {
            yield return new WaitForSeconds(
                waveHoldSeconds
            );
        }

        bool greetingSucceeded = false;

        yield return StartCoroutine(
            PlayGreeting(
                success => greetingSucceeded = success
            )
        );

        if (!greetingSucceeded)
        {
            Debug.LogWarning(
                "Startup greeting failed, but the agent will remain usable."
            );
        }

        isBusy = false;

        SetState(AgentState.Idle);

        Debug.Log(
            "Startup complete. Agent is ready."
        );
    }

    /// <summary>
    /// Plays the pre-recorded greeting for the selected voice.
    /// This never contacts the backend: a missing clip is a wiring
    /// problem and should fail immediately rather than stall on TTS.
    /// </summary>
    private IEnumerator PlayGreeting(
        Action<bool> completed
    )
    {
        VoiceGreeting greetingOverride =
            FindGreetingOverride();

        AudioClip clip =
            greetingOverride != null &&
            greetingOverride.greetingClip != null
                ? greetingOverride.greetingClip
                : LoadGreetingFromResources();

        if (clip == null)
        {
            completed?.Invoke(false);
            yield break;
        }

        if (
            greetingOverride != null &&
            responseText != null &&
            !string.IsNullOrWhiteSpace(
                greetingOverride.greetingText
            )
        )
        {
            responseText.text =
                greetingOverride.greetingText;
        }

        Debug.Log(
            $"Playing greeting '{clip.name}' " +
            $"for voice: {selectedVoiceId}"
        );

        yield return StartCoroutine(
            PlayClip(clip, completed)
        );
    }

    public void StartListening()
    {
        if (isBusy)
        {
            Debug.LogWarning(
                "The agent cannot start listening while processing or speaking."
            );

            return;
        }

        if (isRecording)
        {
            Debug.LogWarning("The microphone is already recording.");
            return;
        }

        if (micRecorder == null)
        {
            HandleError(
                "MicRecorder has not been assigned to ConversationController."
            );

            return;
        }

        try
        {
            isRecording = true;

            SetState(AgentState.Listening);

            micRecorder.StartRecording();

            Debug.Log("Space pressed. Microphone recording started.");
        }
        catch (Exception error)
        {
            isRecording = false;

            HandleError(
                $"Could not start microphone recording: {error.Message}"
            );
        }
    }

    /// <summary>
    /// Stops the microphone, obtains the saved WAV path,
    /// and starts the backend request.
    /// </summary>
    public void StopListeningAndProcess()
    {
        if (!isRecording)
        {
            Debug.LogWarning("Stop recording was called, but no recording is active.");
            return;
        }

        if (micRecorder == null)
        {
            isRecording = false;

            HandleError(
                "MicRecorder has not been assigned to ConversationController."
            );

            return;
        }

        string recordingPath;

        try
        {
            /*
             * MicRecorder.StopRecording() must return the absolute path
             * of the WAV file it saved.
             */
            recordingPath = micRecorder.StopRecording();
        }
        catch (Exception error)
        {
            isRecording = false;

            HandleError(
                $"Could not stop microphone recording: {error.Message}"
            );

            return;
        }

        isRecording = false;

        if (string.IsNullOrWhiteSpace(recordingPath))
        {
            HandleError(
                "MicRecorder stopped, but it did not return a recording path."
            );

            return;
        }

        recordingPath = Path.GetFullPath(recordingPath);

        if (!File.Exists(recordingPath))
        {
            HandleError(
                $"The recording file does not exist: {recordingPath}"
            );

            return;
        }

        Debug.Log($"Recording saved: {recordingPath}");

        ProcessRecording(recordingPath);
    }

    /// <summary>
    /// Starts processing an existing WAV file.
    /// </summary>
    public void ProcessRecording(string recordingPath)
    {
        if (isBusy)
        {
            Debug.LogWarning(
                "The agent is already processing or speaking."
            );

            return;
        }

        if (string.IsNullOrWhiteSpace(recordingPath))
        {
            HandleError("A recording path was not provided.");
            return;
        }

        if (!File.Exists(recordingPath))
        {
            HandleError(
                $"Recording file was not found: {recordingPath}"
            );

            return;
        }

        if (activeConversationCoroutine != null)
        {
            StopCoroutine(activeConversationCoroutine);
        }

        activeConversationCoroutine = StartCoroutine(
            ProcessConversation(recordingPath)
        );
    }

    private IEnumerator ProcessConversation(string recordingPath)
    {

        isBusy = true;
        SetState(AgentState.Thinking);
        StartFillers();

        byte[] audioBytes;

        try
        {
            audioBytes = File.ReadAllBytes(recordingPath);
        }
        catch (Exception error)
        {
            HandleError(
                $"Could not read the recording: {error.Message}"
            );

            activeConversationCoroutine = null;
            yield break;
        }

        if (audioBytes.Length == 0)
        {
            HandleError("The recording file is empty.");

            activeConversationCoroutine = null;
            yield break;
        }

        WWWForm form = new WWWForm();

        form.AddBinaryData(
            uploadFieldName,
            audioBytes,
            Path.GetFileName(recordingPath),
            "audio/wav"
        );

        if (files != null)
        {
            form.AddField(
                "selected_files",
                BuildFilePathsJson(files.GetSelectedFiles())
            );
        }

        string requestUrl = BuildUrl(
            backendBaseUrl,
            agentEndpoint
        );

        Debug.Log($"Sending recording to: {requestUrl}");

        using UnityWebRequest request =
            UnityWebRequest.Post(requestUrl, form);

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            string backendMessage =
                request.downloadHandler != null
                    ? request.downloadHandler.text
                    : string.Empty;

            HandleError(
                $"Backend request failed.\n" +
                $"URL: {requestUrl}\n" +
                $"Status: {request.responseCode}\n" +
                $"Error: {request.error}\n" +
                $"Response: {backendMessage}"
            );

            activeConversationCoroutine = null;
            yield break;
        }

        string responseJson = request.downloadHandler.text;

        if (string.IsNullOrWhiteSpace(responseJson))
        {
            HandleError("The backend returned an empty response.");

            activeConversationCoroutine = null;
            yield break;
        }

        Debug.Log($"Backend response: {responseJson}");

        AgentResponse response;

        try
        {
            response = JsonUtility.FromJson<AgentResponse>(
                responseJson
            );
        }
        catch (Exception error)
        {
            HandleError(
                $"Could not parse the backend response: {error.Message}\n" +
                $"Raw response: {responseJson}"
            );

            activeConversationCoroutine = null;
            yield break;
        }

        if (response == null)
        {
            HandleError(
                $"The backend response could not be deserialised.\n" +
                $"Raw response: {responseJson}"
            );

            activeConversationCoroutine = null;
            yield break;
        }

        UpdateConversationUI(response);

        Debug.Log($"User transcription: {response.transcription}");
        Debug.Log($"Agent response: {response.response_speech}");

        if (response.created_files != null)
        {
            foreach (CreatedFile created in response.created_files)
            {
                Debug.Log($"Created file: {created.path} ({created.status})");
            }
        }

        if (string.IsNullOrWhiteSpace(response.audio_url))
        {
            HandleError(
                "The backend response did not contain an audio_url."
            );

            activeConversationCoroutine = null;
            yield break;
        }

        string fullAudioUrl = BuildUrl(
            backendBaseUrl,
            response.audio_url
        );

        bool audioSucceeded = false;

        yield return StartCoroutine(
            DownloadAndPlayAudio(
                fullAudioUrl,
                success => audioSucceeded = success
            )
        );

        if (!audioSucceeded)
        {
            activeConversationCoroutine = null;
            yield break;
        }

        Debug.Log(
            $"created_files received: " +
            $"{(response.created_files == null ? "null" : response.created_files.Length.ToString())}, " +
            $"files component: {(files == null ? "null" : "assigned")}"
        );

        if (files != null && response.created_files != null)
        {
            foreach (CreatedFile created in response.created_files)
            {
                Debug.Log(
                    $"created_files entry: path='{created.path}' " +
                    $"status='{created.status}' is_new={created.is_new}"
                );

                if (string.IsNullOrWhiteSpace(created.path))
                {
                    continue;
                }

                // Existing files were written straight back to their
                // real path - the user's own editor already picks up
                // the change, no new row or badge needed here.
                if (created.is_new)
                {
                    files.OnFileCreatedByAgent(created.path);
                }
            }
        }

        isBusy = false;
        SetState(AgentState.Idle);

        activeConversationCoroutine = null;

        Debug.Log("Conversation complete. Agent returned to Idle.");
    }

    /// <summary>
    /// Downloads a clip from the backend, then hands it to PlayClip
    /// so streamed and pre-recorded audio behave identically.
    /// </summary>
    private IEnumerator DownloadAndPlayAudio(
        string audioUrl,
        Action<bool> completed
    )
    {
        if (speechAudioSource == null)
        {
            HandleError(
                "Speech AudioSource has not been assigned."
            );

            completed?.Invoke(false);
            yield break;
        }

        Debug.Log($"Downloading response audio: {audioUrl}");

        using UnityWebRequest request =
            UnityWebRequestMultimedia.GetAudioClip(
                audioUrl,
                AudioType.WAV
            );

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            HandleError(
                $"Audio download failed.\n" +
                $"URL: {audioUrl}\n" +
                $"Status: {request.responseCode}\n" +
                $"Error: {request.error}"
            );

            completed?.Invoke(false);
            yield break;
        }

        AudioClip responseClip;

        try
        {
            responseClip =
                DownloadHandlerAudioClip.GetContent(request);
        }
        catch (Exception error)
        {
            HandleError(
                $"Could not create the response AudioClip: {error.Message}"
            );

            completed?.Invoke(false);
            yield break;
        }

        if (responseClip == null)
        {
            HandleError(
                "The response audio downloaded, but the AudioClip was null."
            );

            completed?.Invoke(false);
            yield break;
        }

        StopFillers();

        yield return StartCoroutine(
            PlayClip(responseClip, completed)
        );
    }

    /// <summary>
    /// Plays a clip through the shared speech AudioSource and blocks
    /// until it finishes. Used by both the greeting and the responses.
    /// </summary>
    private IEnumerator PlayClip(
        AudioClip clip,
        Action<bool> completed
    )
    {
        if (speechAudioSource == null)
        {
            HandleError(
                "Speech AudioSource has not been assigned."
            );

            completed?.Invoke(false);
            yield break;
        }

        if (clip == null)
        {
            HandleError(
                "PlayClip was given a null AudioClip."
            );

            completed?.Invoke(false);
            yield break;
        }

        speechAudioSource.Stop();
        speechAudioSource.clip = clip;

        SetState(AgentState.Speaking);

        speechAudioSource.Play();

        if (!speechAudioSource.isPlaying)
        {
            HandleError(
                $"Unity could not start playing the clip: {clip.name}"
            );

            completed?.Invoke(false);
            yield break;
        }

        /*
         * uLipSync analyses this same AudioSource, so pre-recorded
         * greetings get lip sync exactly like streamed responses.
         */
        while (speechAudioSource.isPlaying)
        {
            yield return null;
        }

        completed?.Invoke(true);
    }

    private void UpdateConversationUI(AgentResponse response)
    {
        if (transcriptionText != null)
        {
            transcriptionText.text =
                response.transcription ?? string.Empty;
        }

        if (responseText != null)
        {
            responseText.text =
                response.response_speech ?? string.Empty;
        }

        if (codeText != null)
        {
            codeText.text =
                response.created_files is { Length: > 0 }
                    ? string.Join(
                        "\n",
                        Array.ConvertAll(
                            response.created_files,
                            created => created.path
                        )
                    )
                    : string.Empty;
        }
    }

    private void SetState(AgentState state)
    {
        Debug.Log($"Agent state: {state}");

        if (statusText != null)
        {
            statusText.text = state.ToString();
        }

        if (characterAnimator == null)
        {
            return;
        }

        characterAnimator.SetBool(
            "IsListening",
            state == AgentState.Listening
        );

        characterAnimator.SetBool(
            "IsThinking",
            state == AgentState.Thinking
        );

        characterAnimator.SetBool(
            "IsSpeaking",
            state == AgentState.Speaking
        );
    }

    private void HandleError(string message)
    {
        Debug.LogError(message);

        StopFillers();

        if (speechAudioSource != null)
        {
            speechAudioSource.Stop();
        }

        isBusy = false;
        isRecording = false;

        SetState(AgentState.Idle);
    }

    /// <summary>
    /// Starts looping short filler lines ("Let me dig into that...")
    /// on the speech AudioSource while the backend is processing, so
    /// silence isn't the only feedback during Thinking. Loads
    /// Assets/Resources/Fillers/&lt;voiceId&gt;/*.wav, matching the
    /// greeting clips' folder-per-voice convention.
    /// </summary>
    private void StartFillers()
    {
        if (speechAudioSource == null)
        {
            return;
        }

        StopFillers();

        fillerCoroutine = StartCoroutine(PlayFillerLoop());
    }

    private void StopFillers()
    {
        if (fillerCoroutine != null)
        {
            StopCoroutine(fillerCoroutine);
            fillerCoroutine = null;
        }

        if (speechAudioSource != null)
        {
            speechAudioSource.Stop();
        }
    }

    private IEnumerator PlayFillerLoop()
    {
        AudioClip[] fillerClips =
            Resources.LoadAll<AudioClip>($"Fillers/{selectedVoiceId}");

        if (fillerClips == null || fillerClips.Length == 0)
        {
            yield break;
        }

        // Reacting the instant Space is released feels robotic - a
        // beat of silence first reads as "processing what you said".
        yield return new WaitForSeconds(
            UnityEngine.Random.Range(
                fillerInitialDelayMinSeconds,
                fillerInitialDelayMaxSeconds
            )
        );

        int lastIndex = -1;

        while (true)
        {
            int index = UnityEngine.Random.Range(0, fillerClips.Length);

            if (fillerClips.Length > 1 && index == lastIndex)
            {
                index = (index + 1) % fillerClips.Length;
            }

            lastIndex = index;

            AudioClip clip = fillerClips[index];

            speechAudioSource.clip = clip;
            speechAudioSource.Play();

            yield return new WaitForSeconds(clip.length);

            // Real speech has pauses between thoughts - back-to-back
            // fillers read as unnatural/rapid-fire.
            float gap = UnityEngine.Random.Range(
                fillerMinGapSeconds,
                fillerMaxGapSeconds
            );

            yield return new WaitForSeconds(gap);
        }
    }

    private void ValidateReferences()
    {
        if (micRecorder == null)
        {
            Debug.LogWarning(
                "ConversationController: MicRecorder is not assigned."
            );
        }

        if (speechAudioSource == null)
        {
            Debug.LogWarning(
                "ConversationController: Speech AudioSource is not assigned."
            );
        }

        if (characterAnimator == null)
        {
            Debug.LogWarning(
                "ConversationController: Character Animator is not assigned."
            );
        }

    }

/// <summary>
/// Hand-builds a JSON string array, since JsonUtility cannot
/// serialise a bare List&lt;string&gt;. Values are file paths from
/// the backend's own file listing, so no escaping beyond quotes
/// and backslashes is expected.
/// </summary>
private static string BuildFilePathsJson(List<string> paths)
{
    if (paths == null || paths.Count == 0)
    {
        return "[]";
    }

    IEnumerable<string> escaped = paths.Select(
        path => "\"" +
            path.Replace("\\", "\\\\").Replace("\"", "\\\"") +
            "\""
    );

    return "[" + string.Join(",", escaped) + "]";
}

private static string BuildUrl(
    string baseUrl,
    string pathOrUrl
)
{
    if (string.IsNullOrWhiteSpace(baseUrl))
    {
        throw new ArgumentException(
            "The backend base URL is empty."
        );
    }

    if (string.IsNullOrWhiteSpace(pathOrUrl))
    {
        return baseUrl.TrimEnd('/');
    }

    // Only treat genuine HTTP URLs as complete URLs.
    if (
        pathOrUrl.StartsWith(
            "http://",
            StringComparison.OrdinalIgnoreCase
        ) ||
        pathOrUrl.StartsWith(
            "https://",
            StringComparison.OrdinalIgnoreCase
        )
    )
    {
        return pathOrUrl;
    }

    return
        $"{baseUrl.TrimEnd('/')}/{pathOrUrl.TrimStart('/')}";
}

    private enum AgentState
    {
        Startup,
        Idle,
        Listening,
        Thinking,
        Speaking
    }
}

[Serializable]
public class AgentResponse
{
    public string transcription;
    public string response_speech;
    public string text;
    public string audio_url;
    public CreatedFile[] created_files;
}

[Serializable]
public class CreatedFile
{
    public string path;
    public string status;
    public bool is_new;
}
