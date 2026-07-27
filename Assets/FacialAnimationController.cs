using System.Collections.Generic;
using UnityEngine;

public enum Viseme
{
    Silence,
    Explosive,
    DentalLip,
    Tight,
    Wide,
    Open,
    Rounded,
    Affricate
}

public class FacialAnimationController : MonoBehaviour
{
    [SerializeField]
    private SkinnedMeshRenderer faceRenderer;

    [SerializeField, Range(0f, 100f)]
    private float visemeWeight = 85f;

    private readonly Dictionary<Viseme, int> visemeIndices = new();

    private void Awake()
    {
        if (faceRenderer == null)
        {
            Debug.LogError("Face Renderer has not been assigned.", this);
            enabled = false;
            return;
        }

        CacheBlendShapes();
        ResetMouth();
    }

    public void SetViseme(Viseme viseme)
    {
        ResetMouth();

        if (
            viseme == Viseme.Silence ||
            !visemeIndices.TryGetValue(viseme, out int index)
        )
        {
            return;
        }

        faceRenderer.SetBlendShapeWeight(index, visemeWeight);
    }

    public void ResetMouth()
    {
        foreach (int index in visemeIndices.Values)
        {
            faceRenderer.SetBlendShapeWeight(index, 0f);
        }
    }

    private void CacheBlendShapes()
    {
        AddBlendShape(Viseme.Explosive, "V_Explosive");
        AddBlendShape(Viseme.DentalLip, "V_Dental_Lip");
        AddBlendShape(Viseme.Tight, "V_Tight");
        AddBlendShape(Viseme.Wide, "V_Wide");
        AddBlendShape(Viseme.Open, "V_Open");
        AddBlendShape(Viseme.Rounded, "V_Tight_O");
        AddBlendShape(Viseme.Affricate, "V_Affricate");
    }

    private void AddBlendShape(Viseme viseme, string blendShapeName)
    {
        int index =
            faceRenderer.sharedMesh.GetBlendShapeIndex(blendShapeName);

        if (index < 0)
        {
            Debug.LogWarning(
                $"Blend shape '{blendShapeName}' was not found.",
                faceRenderer
            );

            return;
        }

        visemeIndices[viseme] = index;
    }
}