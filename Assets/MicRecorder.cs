using UnityEngine;
using UnityEngine.InputSystem;

public class MicRecorder : MonoBehaviour
{
    private string micDevice;
    private AudioSource audioSource;
    private bool isRecording = false;

  void Start()
{
    Debug.Log("MicRecorder Start ran");
    Debug.Log("Microphones found: " + Microphone.devices.Length);

    foreach (var device in Microphone.devices)
    {
        Debug.Log("Mic: " + device);
    }

    if (Microphone.devices.Length == 0)
    {
        Debug.LogError("No microphone detected by Unity.");
        return;
    }

    micDevice = Microphone.devices[0];
    audioSource = GetComponent<AudioSource>();

    Debug.Log("Selected mic: " + micDevice);
}

    void Update()
    {
        if (Keyboard.current.spaceKey.wasPressedThisFrame && !isRecording)
        {
            StartRecording();
             Debug.Log("Space pressed");
            Debug.Log("MicRecorder  has Started");
        }

        if (Keyboard.current.spaceKey.wasReleasedThisFrame && isRecording)
        {
            StopRecording();
             Debug.Log("Space was released");
            Debug.Log("MicRecorder has Stopped ");
        }
    }

//     void Update()
// {
//     if (Keyboard.current == null)
//     {
//         Debug.LogError("Keyboard.current is null");
//         return;
//     }

//     if (Keyboard.current.spaceKey.wasPressedThisFrame)
//     {
//         Debug.Log("Space pressed");
//     }

//     if (Keyboard.current.spaceKey.wasReleasedThisFrame)
//     {
//         Debug.Log("Space released");
//     }
// }

    void StartRecording()
    {
        audioSource.clip = Microphone.Start(micDevice, false, 10, AudioSettings.outputSampleRate);
        isRecording = true;
        Debug.Log("Recording started...");
    }

    void StopRecording()
    {
        int recordedSamples = Microphone.GetPosition(micDevice);
        Microphone.End(micDevice);
        isRecording = false;

        AudioClip trimmedClip = TrimClip(audioSource.clip, recordedSamples);
        SavWav.Save("recording", trimmedClip);
        Debug.Log("Recording saved.");
    }

    AudioClip TrimClip(AudioClip clip, int samplesToKeep)
    {
        float[] data = new float[samplesToKeep * clip.channels];
        clip.GetData(data, 0);

        AudioClip trimmed = AudioClip.Create("trimmed", samplesToKeep, clip.channels, clip.frequency, false);
        trimmed.SetData(data, 0);

        return trimmed;
    }
}