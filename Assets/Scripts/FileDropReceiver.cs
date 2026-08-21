using System;
using System.Collections;
using System.IO;
using System.Text;
using Shibuya24.Utility;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Receives files dragged from the OS (Finder/editor) onto the running
/// standalone build via the UniDragAndDropForMac plugin, and imports
/// them into the backend's project directory as agent context.
/// Requires UniDragAndDropForMac to be imported into the project
/// (Mono scripting backend only) - see Assets/Plugins/macOS.
/// </summary>
public class FileDropReceiver : MonoBehaviour
{
    [SerializeField]
    private Files files;

    [Header("Backend")]
    [SerializeField]
    private string backendBaseUrl = "http://127.0.0.1:8000";

    [SerializeField]
    private string importEndpoint = "/files/import";

    private void Start()
    {
        Debug.Log("FileDropReceiver: Start() running, initializing UniDragAndDrop.");

        UniDragAndDrop.Initialize();
        UniDragAndDrop.onDragAndDropFilePath = HandleFileDropped;

        Debug.Log("FileDropReceiver: UniDragAndDrop initialized, callback registered.");
    }

    private void HandleFileDropped(string absolutePath)
    {
        Debug.Log($"FileDropReceiver: native callback fired with path: '{absolutePath}'");

        if (string.IsNullOrWhiteSpace(absolutePath))
        {
            return;
        }

        if (
            !string.Equals(
                Path.GetExtension(absolutePath),
                ".cs",
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            Debug.LogWarning(
                $"Ignored dropped file (only .cs is supported): {absolutePath}"
            );

            return;
        }

        string content;

        try
        {
            content = File.ReadAllText(absolutePath);
        }
        catch (Exception error)
        {
            Debug.LogError(
                $"Could not read dropped file '{absolutePath}': {error.Message}"
            );

            return;
        }

        StartCoroutine(
            ImportFile(
                Path.GetFileName(absolutePath),
                content
            )
        );
    }

    private IEnumerator ImportFile(string fileName, string content)
    {
        string json = JsonUtility.ToJson(
            new ImportFileRequest
            {
                name = fileName,
                content = content,
            }
        );

        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

        string url = backendBaseUrl.TrimEnd('/') + importEndpoint;

        using UnityWebRequest request = new UnityWebRequest(url, "POST");

        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError(
                $"Could not import dropped file '{fileName}': " +
                $"{request.error}\n{request.downloadHandler.text}"
            );

            yield break;
        }

        Debug.Log($"Imported dropped file: {fileName}");

        if (files != null)
        {
            files.OnFileImported(fileName);
        }
    }

    [Serializable]
    private class ImportFileRequest
    {
        public string name;
        public string content;
    }
}
