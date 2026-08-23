using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// A full-screen overlay styled like a video call tile (rounded
/// border, participant name tag, active-speaker border highlight),
/// framing the existing 3D view rather than replacing it - no camera
/// or render texture needed, this just draws on top.
/// </summary>
public class CallTile : MonoBehaviour
{
    [SerializeField]
    private string participantName = "Emmy";

    private VisualElement frame;
    private Label nameLabel;

    private void OnEnable()
    {
        UIDocument document = GetComponent<UIDocument>();

        if (document == null)
        {
            Debug.LogError(
                "CallTile requires a UIDocument component."
            );

            return;
        }

        VisualElement root = document.rootVisualElement;

        frame = root.Q<VisualElement>("call-tile-frame");
        nameLabel = root.Q<Label>("call-tile-name-label");

        if (frame == null || nameLabel == null)
        {
            Debug.LogError(
                "CallTile UI elements could not be found. " +
                "Check the element names in CallTile.uxml."
            );

            return;
        }

        nameLabel.text = participantName;
    }

    public void SetSpeaking(bool isSpeaking)
    {
        if (frame == null)
        {
            return;
        }

        if (isSpeaking)
        {
            frame.AddToClassList("call-tile-frame--speaking");
        }
        else
        {
            frame.RemoveFromClassList("call-tile-frame--speaking");
        }
    }
}
