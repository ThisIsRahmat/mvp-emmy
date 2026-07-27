using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class PhonemeCue
{
    public string phoneme;
    public float start;
    public float end;
}

public class PhonemeSequencePlayer : MonoBehaviour
{
    [SerializeField]
    private FacialAnimationController facialController;

    [SerializeField]
    private AudioSource speechAudioSource;

    private List<PhonemeCue> cues = new();
    private int currentCueIndex;
    private Viseme currentViseme = Viseme.Silence;
    private bool isRunning;

    private void Update()
    {
        if (!isRunning)
        {
            return;
        }

        if (
            speechAudioSource != null &&
            !speechAudioSource.isPlaying
        )
        {
            StopSequence();
            return;
        }

        float currentTime = speechAudioSource != null
            ? speechAudioSource.time
            : 0f;

        ApplyCueAtTime(currentTime);
    }

    public void Play(
        AudioClip speechClip,
        List<PhonemeCue> phonemeCues
    )
    {
        if (speechClip == null)
        {
            Debug.LogError("Speech clip is missing.", this);
            return;
        }

        if (phonemeCues == null || phonemeCues.Count == 0)
        {
            Debug.LogError("Phoneme cues are missing.", this);
            return;
        }

        if (facialController == null)
        {
            Debug.LogError("Facial controller is missing.", this);
            return;
        }

        if (speechAudioSource == null)
        {
            Debug.LogError("Speech AudioSource is missing.", this);
            return;
        }

        cues = phonemeCues;
        currentCueIndex = 0;
        currentViseme = Viseme.Silence;
        isRunning = true;

        facialController.ResetMouth();

        speechAudioSource.clip = speechClip;
        speechAudioSource.Play();
    }

    public void StopSequence()
    {
        isRunning = false;
        currentCueIndex = 0;
        currentViseme = Viseme.Silence;

        if (facialController != null)
        {
            facialController.ResetMouth();
        }
    }

    private void ApplyCueAtTime(float currentTime)
    {
        while (
            currentCueIndex < cues.Count &&
            currentTime >= cues[currentCueIndex].end
        )
        {
            currentCueIndex++;
        }

        if (currentCueIndex >= cues.Count)
        {
            facialController.ResetMouth();
            return;
        }

        PhonemeCue cue = cues[currentCueIndex];

        Viseme targetViseme =
            currentTime >= cue.start &&
            currentTime < cue.end
                ? PhonemeVisemeMapper.ToViseme(cue.phoneme)
                : Viseme.Silence;

        if (targetViseme == currentViseme)
        {
            return;
        }

        currentViseme = targetViseme;
        facialController.SetViseme(targetViseme);
    }

    [ContextMenu("Test Phoneme Sequence")]
    private void TestPhonemeSequence()
    {
        if (facialController == null)
        {
            Debug.LogError(
                "Assign FacialAnimationController in the Inspector.",
                this
            );
            return;
        }

        var testCues = new List<PhonemeCue>
        {
            new PhonemeCue
            {
                phoneme = "HH",
                start = 0.00f,
                end = 0.15f
            },
            new PhonemeCue
            {
                phoneme = "AH",
                start = 0.15f,
                end = 0.35f
            },
            new PhonemeCue
            {
                phoneme = "L",
                start = 0.35f,
                end = 0.50f
            },
            new PhonemeCue
            {
                phoneme = "OW",
                start = 0.50f,
                end = 0.85f
            }
        };

        StartCoroutine(PlayTestWithoutAudio(testCues));
    }

    private IEnumerator PlayTestWithoutAudio(
        List<PhonemeCue> testCues
    )
    {
        isRunning = false;
        facialController.ResetMouth();

        float testTime = 0f;
        int cueIndex = 0;
        Viseme lastTestViseme = Viseme.Silence;

        while (cueIndex < testCues.Count)
        {
            PhonemeCue cue = testCues[cueIndex];

            if (testTime >= cue.end)
            {
                cueIndex++;
                continue;
            }

            Viseme targetViseme =
                testTime >= cue.start
                    ? PhonemeVisemeMapper.ToViseme(cue.phoneme)
                    : Viseme.Silence;

            if (targetViseme != lastTestViseme)
            {
                lastTestViseme = targetViseme;
                facialController.SetViseme(targetViseme);

                Debug.Log(
                    $"{cue.phoneme} → {targetViseme}",
                    this
                );
            }

            testTime += Time.deltaTime;
            yield return null;
        }

        facialController.ResetMouth();
    }
}