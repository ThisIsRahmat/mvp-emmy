using System;
using System.Collections;
using System.Text;
using Shibuya24.Utility;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Receives files dragged from the OS (Finder/editor) onto the running
/// standalone build via the UniDragAndDropForMac plugin, and registers
/// them with the backend as agent context by path. Every participant's
/// project lives somewhere different, so the backend reads the file
/// directly from wherever it was dropped from rather than requiring a
/// shared project folder - any file type is accepted, not just .cs.
/// Requires UniDragAndDropForMac to be imported into the project
/// (Mono scripting backend only) - see Assets/UniDragAndDrop.
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

        StartCoroutine(ImportFile(absolutePath));
    }

    private IEnumerator ImportFile(string absolutePath)
    {
        string json = JsonUtility.ToJson(
            new ImportFileRequest
            {
                path = absolutePath,
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
                $"Could not import dropped file '{absolutePath}': " +
                $"{request.error}\n{request.downloadHandler.text}"
            );

            yield break;
        }

        Debug.Log($"Imported dropped file: {absolutePath}");

        if (files != null)
        {
            files.OnFileImported(absolutePath);
        }
    }

    [Serializable]
    private class ImportFileRequest
    {
        public string path;
    }
}
