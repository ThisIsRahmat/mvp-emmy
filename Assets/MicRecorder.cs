using System;
using System.IO;
using UnityEngine;

public class MicRecorder : MonoBehaviour
{
    [Header("Recording")]
    [SerializeField]
    private int maximumRecordingLengthSeconds = 30;

    private string micDevice;
    private AudioClip recordingClip;
    private bool isRecording;

    public bool IsRecording => isRecording;

    private void Start()
    {

        foreach (string device in Microphone.devices)
        {
            Debug.Log($"Mic: {device}");
        }

        if (Microphone.devices.Length == 0)
        {
            Debug.LogError(
                "No microphone was detected by Unity. " +
                "Check macOS microphone permissions."
            );

            return;
        }

        micDevice = Microphone.devices[0];

        Debug.Log($"Selected mic: {micDevice}");
    }

    public void StartRecording()
    {
        if (isRecording)
        {
            Debug.LogWarning(
                "StartRecording was called while recording was already active."
            );

            return;
        }

        if (string.IsNullOrWhiteSpace(micDevice))
        {
            Debug.LogError(
                "Cannot start recording because no microphone is available."
            );

            return;
        }

        int sampleRate = AudioSettings.outputSampleRate;

        recordingClip = Microphone.Start(
            micDevice,
            false,
            maximumRecordingLengthSeconds,
            sampleRate
        );

        if (recordingClip == null)
        {
            Debug.LogError("Microphone.Start returned a null AudioClip.");
            return;
        }

        isRecording = true;

        Debug.Log(
            $"Recording started using {micDevice} at {sampleRate} Hz."
        );
    }

    public string StopRecording()
    {
        if (!isRecording)
        {
            Debug.LogWarning(
                "StopRecording was called, but no recording is active."
            );

            return null;
        }

        if (string.IsNullOrWhiteSpace(micDevice))
        {
            Debug.LogError(
                "Cannot stop recording because no microphone device is set."
            );

            isRecording = false;
            return null;
        }

        int recordedSamples = Microphone.GetPosition(micDevice);

        Microphone.End(micDevice);

        isRecording = false;

        if (recordedSamples <= 0)
        {
            Debug.LogError(
                $"The microphone returned an invalid sample count: " +
                $"{recordedSamples}"
            );

            recordingClip = null;
            return null;
        }

        if (recordingClip == null)
        {
            Debug.LogError(
                "The recording AudioClip was null when recording stopped."
            );

            return null;
        }

        AudioClip trimmedClip;

        try
        {
            trimmedClip = TrimClip(
                recordingClip,
                recordedSamples
            );
        }
        catch (Exception error)
        {
            Debug.LogError(
                $"Could not trim the recording: {error.Message}"
            );

            return null;
        }

        string recordingName = "recording";

        try
        {
            SavWav.Save(recordingName, trimmedClip);
        }
        catch (Exception error)
        {
            Debug.LogError(
                $"Could not save the WAV recording: {error.Message}"
            );

            return null;
        }

        string recordingPath = GetSavedRecordingPath(recordingName);

        if (!File.Exists(recordingPath))
        {
            Debug.LogError(
                $"SavWav reported success, but the WAV file was not found at: " +
                $"{recordingPath}"
            );

            return null;
        }

        Debug.Log($"Recording saved to: {recordingPath}");

        recordingClip = null;

        return recordingPath;
    }

    private AudioClip TrimClip(
        AudioClip sourceClip,
        int samplesToKeep
    )
    {
        if (sourceClip == null)
        {
            throw new ArgumentNullException(nameof(sourceClip));
        }

        int safeSampleCount = Mathf.Clamp(
            samplesToKeep,
            1,
            sourceClip.samples
        );

        int totalValues =
            safeSampleCount * sourceClip.channels;

        float[] audioData = new float[totalValues];

        bool readSucceeded = sourceClip.GetData(
            audioData,
            0
        );

        if (!readSucceeded)
        {
            throw new InvalidOperationException(
                "AudioClip.GetData failed."
            );
        }

        AudioClip trimmedClip = AudioClip.Create(
            "trimmed_recording",
            safeSampleCount,
            sourceClip.channels,
            sourceClip.frequency,
            false
        );

        bool writeSucceeded = trimmedClip.SetData(
            audioData,
            0
        );

        if (!writeSucceeded)
        {
            throw new InvalidOperationException(
                "AudioClip.SetData failed."
            );
        }

        return trimmedClip;
    }

    private string GetSavedRecordingPath(string recordingName)
    {
        /*
         * The commonly used SavWav implementation saves to:
         *
         * Application.persistentDataPath/recording.wav
         *
         * This must match the implementation inside your SavWav.cs file.
         */
        return Path.Combine(
            Application.persistentDataPath,
            recordingName + ".wav"
        );
    }

    private void OnDisable()
    {
        if (
            isRecording &&
            !string.IsNullOrWhiteSpace(micDevice)
        )
        {
            Microphone.End(micDevice);
            isRecording = false;
        }
    }
}