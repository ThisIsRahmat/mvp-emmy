using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;
using UnityEngine.Networking;

public class Files : MonoBehaviour
{
    private ScrollView filesScroll;
    private Label emptyFilesLabel;

    private VisualElement filesWindow;
    private Button openFilesButton;
    private Button closeFilesButton;

    private VisualElement codeViewer;
    private VisualElement codeHeader;
    private Label codeTitle;
    private TextField codeText;
    private Button closeCodeButton;

    private bool isDraggingCodeViewer;

    private Vector2 codeDragStartPointer;
    private Vector2 codeDragStartPosition;

    private readonly HashSet<string> selectedFiles =
        new HashSet<string>();

    [SerializeField]
    private string backendBaseUrl =
        "http://127.0.0.1:8000";


    private void OnEnable()
    {
        UIDocument document =
            GetComponent<UIDocument>();

        if (document == null)
        {
            Debug.LogError(
                "FilesUI requires a UIDocument component."
            );

            return;
        }

        // Get root FIRST.
        VisualElement root =
            document.rootVisualElement;


        // Find file panel controls.
        filesScroll =
            root.Q<ScrollView>(
                "files-scroll"
            );

        emptyFilesLabel =
            root.Q<Label>(
                "empty-files-label"
            );

        filesWindow =
            root.Q<VisualElement>(
                "files-window"
            );

        openFilesButton =
            root.Q<Button>(
                "open-files-button"
            );

        closeFilesButton =
            root.Q<Button>(
                "close-files-button"
            );


        // Find code viewer controls.
        codeViewer =
            root.Q<VisualElement>(
                "code-viewer"
            );

        codeHeader =
            root.Q<VisualElement>(
                className: "code-header"
            );

        codeTitle =
            root.Q<Label>(
                "code-title"
            );

        codeText =
            root.Q<TextField>(
                "code-text"
            );

        closeCodeButton =
            root.Q<Button>(
                "close-code-button"
            );


        // Validate EVERYTHING before using it.
        if (
            filesScroll == null ||
            filesWindow == null ||
            openFilesButton == null ||
            closeFilesButton == null ||
            codeViewer == null ||
            codeHeader == null ||
            codeTitle == null ||
            codeText == null ||
            closeCodeButton == null
        )
        {
            Debug.LogError(
                "One or more FilesUI elements could not be found. " +
                "Check the names/classes in FilesUI.uxml."
            );

            return;
        }


        Debug.Log(
            $"Files button found: " +
            $"{openFilesButton != null}"
        );


        // Initial state:
        // folder icon visible,
        // file panel hidden,
        // code viewer hidden.
        filesWindow.style.display =
            DisplayStyle.None;

        codeViewer.style.display =
            DisplayStyle.None;

        openFilesButton.style.display =
            DisplayStyle.Flex;


        // Normal button callbacks.
        openFilesButton.clicked +=
            OpenFiles;

        closeFilesButton.clicked +=
            CloseFiles;

        closeCodeButton.clicked +=
            CloseCodeViewer;


        // Code viewer dragging.
        codeHeader.RegisterCallback<PointerDownEvent>(
            OnCodeHeaderPointerDown
        );

        codeHeader.RegisterCallback<PointerMoveEvent>(
            OnCodeHeaderPointerMove
        );

        codeHeader.RegisterCallback<PointerUpEvent>(
            OnCodeHeaderPointerUp
        );

        codeHeader.RegisterCallback<PointerCaptureOutEvent>(
            OnCodeHeaderPointerCaptureOut
        );


        // Prevent clicking X from also starting a drag.
        closeCodeButton.RegisterCallback<PointerDownEvent>(
            OnCloseCodePointerDown
        );


        StartCoroutine(
    LoadFilesFromBackend()
);



    }




    private void OpenFiles()
    {
        Debug.Log(
            "Files panel opened."
        );

        filesWindow.style.display =
            DisplayStyle.Flex;

        openFilesButton.style.display =
            DisplayStyle.None;

        codeViewer.style.display =
            DisplayStyle.None;
    }


    private void CloseFiles()
    {
        filesWindow.style.display =
            DisplayStyle.None;

        codeViewer.style.display =
            DisplayStyle.None;

        openFilesButton.style.display =
            DisplayStyle.Flex;
    }


    private void OpenCodeViewer(
        string fileName
    )
    {
        codeTitle.text =
            fileName;

        codeText.value =
            $"// Temporary preview for {fileName}\n\n" +
            "// Real file contents will be loaded here next.";

        // Keep the Files panel visible.
        filesWindow.style.display =
            DisplayStyle.Flex;

        codeViewer.style.display =
            DisplayStyle.Flex;

        openFilesButton.style.display =
            DisplayStyle.None;
    }


    private void CloseCodeViewer()
    {
        codeViewer.style.display =
            DisplayStyle.None;

        filesWindow.style.display =
            DisplayStyle.Flex;
    }

    /// load files from backend
    private IEnumerator LoadFilesFromBackend()
        
    {
        string url =
            $"{backendBaseUrl}/files";

        using UnityWebRequest request =
            UnityWebRequest.Get(url);

        yield return request.SendWebRequest();

        if (
            request.result !=
            UnityWebRequest.Result.Success
        )
        {
            Debug.LogError(
                $"Could not load files: " +
                $"{request.error}\n" +
                $"{request.downloadHandler.text}"
            );

            yield break;
        }

        FileListResponse response =
            JsonUtility.FromJson<FileListResponse>(
                request.downloadHandler.text
            );

        filesScroll.Clear();

        if (
            response == null ||
            response.files == null ||
            response.files.Length == 0
        )
        {
            if (emptyFilesLabel != null)
            {
                emptyFilesLabel.style.display =
                    DisplayStyle.Flex;
            }

            yield break;
        }

        if (emptyFilesLabel != null)
        {
            emptyFilesLabel.style.display =
                DisplayStyle.None;
        }

        foreach (
            ProjectFile file in response.files
        )
        {
            AddFileRow(
                file.name,
                file.path
            );
        }
    }

    // add file content 

    private IEnumerator LoadFileContent(
    string fileName,
    string filePath
    )
    {
        string encodedPath =
            UnityWebRequest.EscapeURL(
                filePath
            );

        string url =
            $"{backendBaseUrl}/files/content" +
            $"?path={encodedPath}";

        using UnityWebRequest request =
            UnityWebRequest.Get(url);

        yield return request.SendWebRequest();

        if (
            request.result !=
            UnityWebRequest.Result.Success
        )
        {
            Debug.LogError(
                $"Could not load file content: " +
                $"{request.error}\n" +
                $"{request.downloadHandler.text}"
            );

            yield break;
        }

        FileContentResponse response =
            JsonUtility.FromJson<FileContentResponse>(
                request.downloadHandler.text
            );

        codeTitle.text =
            fileName;

        codeText.value =
            response.content;

        filesWindow.style.display =
            DisplayStyle.Flex;

        codeViewer.style.display =
            DisplayStyle.Flex;

        openFilesButton.style.display =
            DisplayStyle.None;
    }


    
    // CODE VIEWER DRAGGING
   

    private void OnCodeHeaderPointerDown(
        PointerDownEvent evt
    )
    {
        // Only left mouse button.
        if (evt.button != 0)
        {
            return;
        }

        isDraggingCodeViewer = true;

        codeDragStartPointer =
            evt.position;

        codeDragStartPosition =
            new Vector2(
                codeViewer.resolvedStyle.left,
                codeViewer.resolvedStyle.top
            );

        codeHeader.CapturePointer(
            evt.pointerId
        );

        evt.StopPropagation();
    }


    private void OnCodeHeaderPointerMove(
        PointerMoveEvent evt
    )
    {
        if (
            !isDraggingCodeViewer ||
            !codeHeader.HasPointerCapture(
                evt.pointerId
            )
        )
        {
            return;
        }

        Vector2 currentPointer =
    new Vector2(
        evt.position.x,
        evt.position.y
    );

Vector2 delta =
    currentPointer -
    codeDragStartPointer;

        codeViewer.style.left =
            codeDragStartPosition.x +
            delta.x;

        codeViewer.style.top =
            codeDragStartPosition.y +
            delta.y;

        evt.StopPropagation();
    }


    private void OnCodeHeaderPointerUp(
        PointerUpEvent evt
    )
    {
        if (!isDraggingCodeViewer)
        {
            return;
        }

        isDraggingCodeViewer = false;

        if (
            codeHeader.HasPointerCapture(
                evt.pointerId
            )
        )
        {
            codeHeader.ReleasePointer(
                evt.pointerId
            );
        }

        evt.StopPropagation();
    }


    private void OnCodeHeaderPointerCaptureOut(
        PointerCaptureOutEvent evt
    )
    {
        isDraggingCodeViewer = false;
    }


    private void OnCloseCodePointerDown(
        PointerDownEvent evt
    )
    {
        // Prevent the close button click
        // bubbling into the draggable header.
        evt.StopPropagation();
    }


    // ----------------------------
    // FILE ROWS
    // ----------------------------

    private void AddFileRow(
    string fileName,
    string filePath
    )
    {
        if (emptyFilesLabel != null)
        {
            emptyFilesLabel.style.display =
                DisplayStyle.None;
        }

        VisualElement row =
            new VisualElement();

        row.AddToClassList(
            "file-row"
        );


        Toggle toggle =
            new Toggle();

        toggle.AddToClassList(
            "file-toggle"
        );


        Label name =
            new Label(fileName);

        name.AddToClassList(
            "file-name"
        );


        Label statusBadge =
            new Label();

        statusBadge.AddToClassList(
            "file-status"
        );

        statusBadge.style.display =
            DisplayStyle.None;


        Button viewButton =
        new Button(
            () =>
            {
                StartCoroutine(
                    LoadFileContent(
                        fileName,
                        filePath
                    )
                );
            }
        );

        viewButton.text =
            "View";

        viewButton.AddToClassList(
            "view-button"
        );


        toggle.RegisterValueChangedCallback(
            evt =>
            {
                if (evt.newValue)
                {
                    selectedFiles.Add(
                         filePath
                    );
                }
                else
                {
                    selectedFiles.Remove(
                         filePath
                    );
                }

                Debug.Log(
                    $"Selected context files: " +
                    $"{string.Join(", ", selectedFiles)}"
                );
            }
        );


        row.Add(toggle);
        row.Add(name);
        row.Add(statusBadge);
        row.Add(viewButton);

        filesScroll.Add(row);
    }


    public List<string> GetSelectedFiles()
    {
        return new List<string>(
            selectedFiles
        );
    }

    public void RefreshFiles()
    {
        StartCoroutine(
            LoadFilesFromBackend()
        );
    }



    private void OnDisable()
    {
        if (openFilesButton != null)
        {
            openFilesButton.clicked -=
                OpenFiles;
        }

        if (closeFilesButton != null)
        {
            closeFilesButton.clicked -=
                CloseFiles;
        }

        if (closeCodeButton != null)
        {
            closeCodeButton.clicked -=
                CloseCodeViewer;

            closeCodeButton.UnregisterCallback<
                PointerDownEvent
            >(
                OnCloseCodePointerDown
            );
        }

        if (codeHeader != null)
        {
            codeHeader.UnregisterCallback<
                PointerDownEvent
            >(
                OnCodeHeaderPointerDown
            );

            codeHeader.UnregisterCallback<
                PointerMoveEvent
            >(
                OnCodeHeaderPointerMove
            );

            codeHeader.UnregisterCallback<
                PointerUpEvent
            >(
                OnCodeHeaderPointerUp
            );

            codeHeader.UnregisterCallback<
                PointerCaptureOutEvent
            >(
                OnCodeHeaderPointerCaptureOut
            );
        }


    }

}

[System.Serializable]
public class FileListResponse
{
    public ProjectFile[] files;
}

[System.Serializable]
public class ProjectFile
{
    public string name;
    public string path;
}

[System.Serializable]
public class FileContentResponse
{
    public string path;
    public string content;
}