using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using TMPro;

public class ConversationController : MonoBehaviour
{
    [Header("Backend")]
    [SerializeField]
    private string backendBaseUrl = "http://127.0.0.1:8000";

    [Header("Audio")]
    [SerializeField]
    private AudioSource speechAudioSource;

    [Header("Embodiment")]
    [SerializeField]
    private AudioDrivenLipSync lipSync;

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

    private bool isBusy;

    public void ProcessRecording(string recordingPath)
    {
        if (isBusy)
        {
            Debug.LogWarning("The agent is already processing a request.");
            return;
        }

        StartCoroutine(ProcessConversation(recordingPath));
    }

    private IEnumerator ProcessConversation(string recordingPath)
    {
        isBusy = true;

        SetState("Thinking");

        byte[] audioBytes;

        try
        {
            audioBytes = File.ReadAllBytes(recordingPath);
        }
        catch (Exception error)
        {
            HandleError($"Could not read recording: {error.Message}");
            yield break;
        }

        WWWForm form = new WWWForm();
        form.AddBinaryData(
            "audio",
            audioBytes,
            "recording.wav",
            "audio/wav"
        );

        using UnityWebRequest request = UnityWebRequest.Post(
            $"{backendBaseUrl}/agent/respond",
            form
        );

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            HandleError(
                $"Backend request failed: {request.error}\n" +
                request.downloadHandler.text
            );

            yield break;
        }

        AgentResponse response;

        try
        {
            response = JsonUtility.FromJson<AgentResponse>(
                request.downloadHandler.text
            );
        }
        catch (Exception error)
        {
            HandleError($"Could not parse response: {error.Message}");
            yield break;
        }

        transcriptionText.text = response.transcription;
        responseText.text = response.response_speech;
        codeText.text = response.response_code;

        string fullAudioUrl = backendBaseUrl + response.audio_url;

        yield return StartCoroutine(
            DownloadAndPlayAudio(fullAudioUrl)
        );

        isBusy = false;
        SetState("Idle");
    }

    private IEnumerator DownloadAndPlayAudio(string audioUrl)
    {
        using UnityWebRequest request =
            UnityWebRequestMultimedia.GetAudioClip(
                audioUrl,
                AudioType.WAV
            );

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            HandleError($"Audio download failed: {request.error}");
            yield break;
        }

        AudioClip clip = DownloadHandlerAudioClip.GetContent(request);

        speechAudioSource.clip = clip;

        SetState("Speaking");

        speechAudioSource.Play();

        while (speechAudioSource.isPlaying)
        {
            yield return null;
        }
    }

    private void SetState(string state)
    {
        if (statusText != null)
        {
            statusText.text = state;
        }

        if (characterAnimator == null)
        {
            return;
        }

        characterAnimator.SetBool(
            "IsListening",
            state == "Listening"
        );

        characterAnimator.SetBool(
            "IsThinking",
            state == "Thinking"
        );

        characterAnimator.SetBool(
            "IsSpeaking",
            state == "Speaking"
        );
    }

    private void HandleError(string message)
    {
        Debug.LogError(message);

        if (statusText != null)
        {
            statusText.text = "Error";
        }

        isBusy = false;
        SetState("Idle");
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