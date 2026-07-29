using UnityEngine;

public class AudioDrivenLipSync : MonoBehaviour
{
    [SerializeField]
    private AudioSource audioSource;

    [SerializeField]
    private SkinnedMeshRenderer faceRenderer;

    [SerializeField]
    private string mouthOpenBlendShape = "V_Open";

    [SerializeField]
    private float sensitivity = 100f;

    [SerializeField]
    private float smoothing = 12f;

    private const int SampleSize = 256;

    private readonly float[] samples = new float[SampleSize];

    private int mouthOpenIndex = -1;

    private float currentWeight;

    private void Awake()
    {
        if (faceRenderer == null)
        {
            Debug.LogError(
                "AudioDrivenLipSync: Face renderer is not assigned."
            );

            enabled = false;
            return;
        }

        mouthOpenIndex = faceRenderer.sharedMesh.GetBlendShapeIndex(
            mouthOpenBlendShape
        );

        if (mouthOpenIndex < 0)
        {
            Debug.LogError(
                $"AudioDrivenLipSync: Blend shape " +
                $"'{mouthOpenBlendShape}' was not found."
            );

            enabled = false;
        }
    }

    private void Update()
    {
        if (
            audioSource == null ||
            faceRenderer == null ||
            mouthOpenIndex < 0
        )
        {
            return;
        }

        float targetWeight = 0f;

        if (audioSource.isPlaying)
        {
            audioSource.GetOutputData(samples, 0);

            float sum = 0f;

            for (int i = 0; i < samples.Length; i++)
            {
                sum += samples[i] * samples[i];
            }

            float rms = Mathf.Sqrt(sum / samples.Length);

            targetWeight = Mathf.Clamp01(
                rms * sensitivity
            ) * 100f;
        }

        currentWeight = Mathf.Lerp(
            currentWeight,
            targetWeight,
            Time.deltaTime * smoothing
        );

        faceRenderer.SetBlendShapeWeight(
            mouthOpenIndex,
            currentWeight
        );
    }
}