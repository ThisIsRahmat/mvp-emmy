using System;
using System.Collections;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Networking;

public class ConversationController : MonoBehaviour
{
    [Header("Backend")]
    [SerializeField]
    private string backendBaseUrl = "http://127.0.0.1:8000";

    [SerializeField]
    private string agentEndpoint = "/agent/respond";

    [SerializeField]
    private string uploadFieldName = "audio";

    [Header("Microphone")]
    [SerializeField]
    private MicRecorder micRecorder;

    [Header("Audio")]
    [SerializeField]
    private AudioSource speechAudioSource;

    [SerializeField]
    private AudioClip startupGreeting;

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

    private Coroutine activeConversationCoroutine;

    private void Awake()
    {
        ValidateReferences();
    }

    private void Start()
    {
        StartCoroutine(PlayStartupSequence());
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

        if (speechAudioSource != null && startupGreeting != null)
        {
            speechAudioSource.Stop();
            speechAudioSource.clip = startupGreeting;
            speechAudioSource.Play();

            while (speechAudioSource.isPlaying)
            {
                yield return null;
            }
        }
        else
        {
            Debug.LogWarning(
                "Startup greeting could not play because the AudioSource " +
                "or startup AudioClip has not been assigned."
            );

            yield return new WaitForSeconds(1f);
        }

        isBusy = false;
        SetState(AgentState.Idle);

        Debug.Log("Startup complete. Agent is ready.");
    }

    /// <summary>
    /// Starts the push-to-talk interaction.
    /// This may also be called from a Unity UI button.
    /// </summary>
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
    /// This may also be called from a Unity UI button.
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

        if (!string.IsNullOrWhiteSpace(response.response_code))
        {
            Debug.Log($"Generated code:\n{response.response_code}");
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

        isBusy = false;
        SetState(AgentState.Idle);

        activeConversationCoroutine = null;

        Debug.Log("Conversation complete. Agent returned to Idle.");
    }

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

        speechAudioSource.Stop();
        speechAudioSource.clip = responseClip;

        SetState(AgentState.Speaking);

        speechAudioSource.Play();

        if (!speechAudioSource.isPlaying)
        {
            HandleError(
                "Unity could not start playing the downloaded response audio."
            );

            completed?.Invoke(false);
            yield break;
        }

        /*
         * uLipSync should analyse this same AudioSource automatically
         * while the response is playing.
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
                response.response_code ?? string.Empty;
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

        if (speechAudioSource != null)
        {
            speechAudioSource.Stop();
        }

        isBusy = false;
        isRecording = false;

        SetState(AgentState.Idle);
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
    public string response_code;
    public string audio_url;
}