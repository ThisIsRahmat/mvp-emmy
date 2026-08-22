using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;
using UnityEngine.Networking;

public class Files : MonoBehaviour
{
    private ScrollView filesScroll;
    private Label emptyFilesLabel;

    private VisualElement filesWindow;
    private VisualElement filesHeader;
    private Button openFilesButton;
    private Button closeFilesButton;
    private Label dropToastLabel;
    private VisualElement filesResizeHandle;

    private VisualElement codeViewer;
    private VisualElement codeHeader;
    private ScrollView codeScroll;
    private Label codeTitle;
    private TextField codeText;
    private Button closeCodeButton;
    private VisualElement codeResizeHandle;

    private bool isDraggingCodeViewer;

    private Vector2 codeDragStartPointer;
    private Vector2 codeDragStartPosition;

    private bool isResizingCodeViewer;

    private Vector2 codeResizeDragStartPointer;
    private Vector2 codeResizeStartSize;

    private const float MinCodeViewerWidth = 300f;
    private const float MinCodeViewerHeight = 260f;

    private bool isDraggingFilesWindow;

    private Vector2 filesDragStartPointer;
    private Vector2 filesDragStartPosition;

    private bool isResizingFilesWindow;

    private Vector2 resizeDragStartPointer;
    private Vector2 resizeStartSize;

    private const float MinFilesWindowWidth = 260f;
    private const float MinFilesWindowHeight = 220f;

    /// <summary>
    /// How much of a dragged window must stay within the visible
    /// root area, so its header/close button can never be dragged
    /// fully out of reach.
    /// </summary>
    private const float MinVisibleWindowMargin = 60f;

    private VisualElement uiRoot;

    private Coroutine dropToastCoroutine;

    private readonly List<string> loadedFilePaths =
        new List<string>();

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

        uiRoot = root;


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

        filesHeader =
            root.Q<VisualElement>(
                "files-header"
            );

        filesResizeHandle =
            root.Q<VisualElement>(
                "files-resize-handle"
            );

        openFilesButton =
            root.Q<Button>(
                "open-files-button"
            );

        closeFilesButton =
            root.Q<Button>(
                "close-files-button"
            );

        dropToastLabel =
            root.Q<Label>(
                "drop-toast-label"
            );


        // Find code viewer controls.
        codeViewer =
            root.Q<VisualElement>(
                "code-viewer"
            );

        codeHeader =
            root.Q<VisualElement>(
                "code-header"
            );

        codeTitle =
            root.Q<Label>(
                "code-title"
            );

        codeText =
            root.Q<TextField>(
                "code-text"
            );

        codeScroll =
            root.Q<ScrollView>(
                "code-scroll"
            );

        closeCodeButton =
            root.Q<Button>(
                "close-code-button"
            );

        codeResizeHandle =
            root.Q<VisualElement>(
                "code-resize-handle"
            );


        // Validate EVERYTHING before using it.
        if (
            filesScroll == null ||
            filesWindow == null ||
            filesHeader == null ||
            filesResizeHandle == null ||
            openFilesButton == null ||
            closeFilesButton == null ||
            codeViewer == null ||
            codeHeader == null ||
            codeTitle == null ||
            codeText == null ||
            codeScroll == null ||
            closeCodeButton == null ||
            codeResizeHandle == null
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

        if (dropToastLabel != null)
        {
            dropToastLabel.style.display =
                DisplayStyle.None;
        }


        // Normal button callbacks.
        openFilesButton.clicked +=
            OpenFiles;

        closeFilesButton.clicked +=
            CloseFiles;

        closeCodeButton.clicked +=
            CloseCodeViewer;


        // Code viewer dragging - draggable from anywhere in the
        // window body except the code text, the close button and
        // the resize handle, which need their own pointer input.
        codeViewer.RegisterCallback<PointerDownEvent>(
            OnCodeViewerPointerDown
        );

        codeViewer.RegisterCallback<PointerMoveEvent>(
            OnCodeViewerPointerMove
        );

        codeViewer.RegisterCallback<PointerUpEvent>(
            OnCodeViewerPointerUp
        );

        codeViewer.RegisterCallback<PointerCaptureOutEvent>(
            OnCodeViewerPointerCaptureOut
        );


        // Code viewer resizing.
        codeResizeHandle.RegisterCallback<PointerDownEvent>(
            OnCodeResizeHandlePointerDown
        );

        codeResizeHandle.RegisterCallback<PointerMoveEvent>(
            OnCodeResizeHandlePointerMove
        );

        codeResizeHandle.RegisterCallback<PointerUpEvent>(
            OnCodeResizeHandlePointerUp
        );

        codeResizeHandle.RegisterCallback<PointerCaptureOutEvent>(
            OnCodeResizeHandlePointerCaptureOut
        );


        // Files window dragging - draggable from anywhere in the
        // window body except the scrollable list, the close button
        // and the resize handle, which need their own pointer input.
        filesWindow.RegisterCallback<PointerDownEvent>(
            OnFilesWindowPointerDown
        );

        filesWindow.RegisterCallback<PointerMoveEvent>(
            OnFilesWindowPointerMove
        );

        filesWindow.RegisterCallback<PointerUpEvent>(
            OnFilesWindowPointerUp
        );

        filesWindow.RegisterCallback<PointerCaptureOutEvent>(
            OnFilesWindowPointerCaptureOut
        );


        // Files window resizing.
        filesResizeHandle.RegisterCallback<PointerDownEvent>(
            OnFilesResizeHandlePointerDown
        );

        filesResizeHandle.RegisterCallback<PointerMoveEvent>(
            OnFilesResizeHandlePointerMove
        );

        filesResizeHandle.RegisterCallback<PointerUpEvent>(
            OnFilesResizeHandlePointerUp
        );

        filesResizeHandle.RegisterCallback<PointerCaptureOutEvent>(
            OnFilesResizeHandlePointerCaptureOut
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
        loadedFilePaths.Clear();

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
   

    private bool IsExcludedFromCodeViewerDrag(VisualElement target)
    {
        if (target == null)
        {
            return false;
        }

        // TextField/Button are composite controls, so a click can
        // target an internal child rather than the control itself -
        // check containment, not just reference equality.
        return
            IsSameOrDescendant(closeCodeButton, target) ||
            IsSameOrDescendant(codeResizeHandle, target) ||
            IsSameOrDescendant(codeScroll, target);
    }

    private static bool IsSameOrDescendant(
        VisualElement ancestor,
        VisualElement target
    )
    {
        return
            ancestor != null &&
            (ancestor == target || ancestor.Contains(target));
    }

    private void OnCodeViewerPointerDown(PointerDownEvent evt)
    {
        if (evt.button != 0)
        {
            return;
        }

        if (IsExcludedFromCodeViewerDrag(evt.target as VisualElement))
        {
            return;
        }

        isDraggingCodeViewer = true;

        codeDragStartPointer = evt.position;

        codeDragStartPosition = new Vector2(
            codeViewer.resolvedStyle.left,
            codeViewer.resolvedStyle.top
        );

        codeViewer.CapturePointer(evt.pointerId);

        evt.StopPropagation();
    }

    private void OnCodeViewerPointerMove(PointerMoveEvent evt)
    {
        if (
            !isDraggingCodeViewer ||
            !codeViewer.HasPointerCapture(evt.pointerId)
        )
        {
            return;
        }

        Vector2 currentPointer = new Vector2(evt.position.x, evt.position.y);
        Vector2 delta = currentPointer - codeDragStartPointer;

        Vector2 clamped = ClampWindowPosition(
            codeViewer,
            codeDragStartPosition.x + delta.x,
            codeDragStartPosition.y + delta.y
        );

        codeViewer.style.left = clamped.x;
        codeViewer.style.top = clamped.y;

        evt.StopPropagation();
    }

    private void OnCodeViewerPointerUp(PointerUpEvent evt)
    {
        if (!isDraggingCodeViewer)
        {
            return;
        }

        isDraggingCodeViewer = false;

        if (codeViewer.HasPointerCapture(evt.pointerId))
        {
            codeViewer.ReleasePointer(evt.pointerId);
        }

        evt.StopPropagation();
    }

    private void OnCodeViewerPointerCaptureOut(PointerCaptureOutEvent evt)
    {
        isDraggingCodeViewer = false;
    }


    // ----------------------------
    // CODE VIEWER RESIZING
    // ----------------------------

    private void OnCodeResizeHandlePointerDown(PointerDownEvent evt)
    {
        if (evt.button != 0)
        {
            return;
        }

        isResizingCodeViewer = true;

        codeResizeDragStartPointer = evt.position;

        codeResizeStartSize = new Vector2(
            codeViewer.resolvedStyle.width,
            codeViewer.resolvedStyle.height
        );

        codeResizeHandle.CapturePointer(evt.pointerId);

        evt.StopPropagation();
    }

    private void OnCodeResizeHandlePointerMove(PointerMoveEvent evt)
    {
        if (
            !isResizingCodeViewer ||
            !codeResizeHandle.HasPointerCapture(evt.pointerId)
        )
        {
            return;
        }

        Vector2 currentPointer = new Vector2(evt.position.x, evt.position.y);
        Vector2 delta = currentPointer - codeResizeDragStartPointer;

        codeViewer.style.width = Mathf.Max(
            MinCodeViewerWidth,
            codeResizeStartSize.x + delta.x
        );

        codeViewer.style.height = Mathf.Max(
            MinCodeViewerHeight,
            codeResizeStartSize.y + delta.y
        );

        evt.StopPropagation();
    }

    private void OnCodeResizeHandlePointerUp(PointerUpEvent evt)
    {
        if (!isResizingCodeViewer)
        {
            return;
        }

        isResizingCodeViewer = false;

        if (codeResizeHandle.HasPointerCapture(evt.pointerId))
        {
            codeResizeHandle.ReleasePointer(evt.pointerId);
        }

        evt.StopPropagation();
    }

    private void OnCodeResizeHandlePointerCaptureOut(PointerCaptureOutEvent evt)
    {
        isResizingCodeViewer = false;
    }


    /// <summary>
    /// Keeps at least MinVisibleWindowMargin worth of a dragged
    /// window within the visible root area on every side, so its
    /// header/close button can never end up fully off-screen and
    /// unreachable.
    /// </summary>
    private Vector2 ClampWindowPosition(
        VisualElement window,
        float proposedLeft,
        float proposedTop
    )
    {
        if (uiRoot == null)
        {
            return new Vector2(proposedLeft, proposedTop);
        }

        float rootWidth = uiRoot.resolvedStyle.width;
        float rootHeight = uiRoot.resolvedStyle.height;
        float windowWidth = window.resolvedStyle.width;
        float windowHeight = window.resolvedStyle.height;

        float minLeft = MinVisibleWindowMargin - windowWidth;
        float maxLeft = rootWidth - MinVisibleWindowMargin;

        float minTop = 0f;
        float maxTop = rootHeight - MinVisibleWindowMargin;

        float clampedLeft = Mathf.Clamp(
            proposedLeft,
            Mathf.Min(minLeft, maxLeft),
            Mathf.Max(minLeft, maxLeft)
        );

        float clampedTop = Mathf.Clamp(
            proposedTop,
            Mathf.Min(minTop, maxTop),
            Mathf.Max(minTop, maxTop)
        );

        return new Vector2(clampedLeft, clampedTop);
    }


    // ----------------------------
    // FILES WINDOW DRAGGING
    // ----------------------------

    /// <summary>
    /// The scroll list, close button and resize handle need their own
    /// pointer input (scrolling, clicking, resizing) rather than
    /// moving the window, so a drag starting on any of them is
    /// ignored here.
    /// </summary>
    private bool IsExcludedFromWindowDrag(VisualElement target)
    {
        if (target == null)
        {
            return false;
        }

        return
            IsSameOrDescendant(closeFilesButton, target) ||
            IsSameOrDescendant(filesResizeHandle, target) ||
            IsSameOrDescendant(filesScroll, target);
    }

    private void OnFilesWindowPointerDown(PointerDownEvent evt)
    {
        if (evt.button != 0)
        {
            return;
        }

        if (IsExcludedFromWindowDrag(evt.target as VisualElement))
        {
            return;
        }

        isDraggingFilesWindow = true;

        filesDragStartPointer = evt.position;

        filesDragStartPosition = new Vector2(
            filesWindow.resolvedStyle.left,
            filesWindow.resolvedStyle.top
        );

        filesWindow.CapturePointer(evt.pointerId);

        evt.StopPropagation();
    }

    private void OnFilesWindowPointerMove(PointerMoveEvent evt)
    {
        if (
            !isDraggingFilesWindow ||
            !filesWindow.HasPointerCapture(evt.pointerId)
        )
        {
            return;
        }

        Vector2 currentPointer = new Vector2(evt.position.x, evt.position.y);
        Vector2 delta = currentPointer - filesDragStartPointer;

        Vector2 clamped = ClampWindowPosition(
            filesWindow,
            filesDragStartPosition.x + delta.x,
            filesDragStartPosition.y + delta.y
        );

        filesWindow.style.left = clamped.x;
        filesWindow.style.top = clamped.y;

        evt.StopPropagation();
    }

    private void OnFilesWindowPointerUp(PointerUpEvent evt)
    {
        if (!isDraggingFilesWindow)
        {
            return;
        }

        isDraggingFilesWindow = false;

        if (filesWindow.HasPointerCapture(evt.pointerId))
        {
            filesWindow.ReleasePointer(evt.pointerId);
        }

        evt.StopPropagation();
    }

    private void OnFilesWindowPointerCaptureOut(PointerCaptureOutEvent evt)
    {
        isDraggingFilesWindow = false;
    }


    // ----------------------------
    // FILES WINDOW RESIZING
    // ----------------------------

    private void OnFilesResizeHandlePointerDown(PointerDownEvent evt)
    {
        if (evt.button != 0)
        {
            return;
        }

        isResizingFilesWindow = true;

        resizeDragStartPointer = evt.position;

        resizeStartSize = new Vector2(
            filesWindow.resolvedStyle.width,
            filesWindow.resolvedStyle.height
        );

        filesResizeHandle.CapturePointer(evt.pointerId);

        evt.StopPropagation();
    }

    private void OnFilesResizeHandlePointerMove(PointerMoveEvent evt)
    {
        if (
            !isResizingFilesWindow ||
            !filesResizeHandle.HasPointerCapture(evt.pointerId)
        )
        {
            return;
        }

        Vector2 currentPointer = new Vector2(evt.position.x, evt.position.y);
        Vector2 delta = currentPointer - resizeDragStartPointer;

        filesWindow.style.width = Mathf.Max(
            MinFilesWindowWidth,
            resizeStartSize.x + delta.x
        );

        filesWindow.style.height = Mathf.Max(
            MinFilesWindowHeight,
            resizeStartSize.y + delta.y
        );

        evt.StopPropagation();
    }

    private void OnFilesResizeHandlePointerUp(PointerUpEvent evt)
    {
        if (!isResizingFilesWindow)
        {
            return;
        }

        isResizingFilesWindow = false;

        if (filesResizeHandle.HasPointerCapture(evt.pointerId))
        {
            filesResizeHandle.ReleasePointer(evt.pointerId);
        }

        evt.StopPropagation();
    }

    private void OnFilesResizeHandlePointerCaptureOut(PointerCaptureOutEvent evt)
    {
        isResizingFilesWindow = false;
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

        loadedFilePaths.Add(
            filePath
        );

        VisualElement row =
            new VisualElement();

        row.AddToClassList(
            "file-row"
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


        row.Add(name);
        row.Add(statusBadge);
        row.Add(viewButton);

        filesScroll.Add(row);
    }


    public List<string> GetSelectedFiles()
    {
        return new List<string>(
            loadedFilePaths
        );
    }

    public void RefreshFiles()
    {
        StartCoroutine(
            LoadFilesFromBackend()
        );
    }

    /// <summary>
    /// Called by FileDropReceiver once a dropped file has been
    /// registered with the backend by path. Flashes the filename at
    /// the drop-target icon, then adds it to the list directly -
    /// dropped files live wherever the participant's own project is,
    /// not under a folder the backend can rescan, so this adds the
    /// row itself instead of calling RefreshFiles().
    /// </summary>
    public void OnFileImported(string filePath)
    {
        if (dropToastCoroutine != null)
        {
            StopCoroutine(dropToastCoroutine);
        }

        dropToastCoroutine = StartCoroutine(
            ShowDropToastThenAddRow(filePath)
        );
    }

    /// <summary>
    /// Flashes the filename and pops the folder icon to acknowledge
    /// the drop, then adds the file to the list without opening the
    /// panel.
    /// </summary>
    private IEnumerator ShowDropToastThenAddRow(string filePath)
    {
        string displayName = Path.GetFileName(filePath);

        if (dropToastLabel != null)
        {
            dropToastLabel.text = displayName;
            dropToastLabel.style.display = DisplayStyle.Flex;
        }

        if (openFilesButton != null)
        {
            openFilesButton.text = "📂";
            openFilesButton.AddToClassList("files-icon-button--drop-active");
        }

        yield return new WaitForSeconds(1.2f);

        if (dropToastLabel != null)
        {
            dropToastLabel.style.display = DisplayStyle.None;
        }

        if (openFilesButton != null)
        {
            openFilesButton.text = "📁";
            openFilesButton.RemoveFromClassList("files-icon-button--drop-active");
        }

        if (!loadedFilePaths.Contains(filePath))
        {
            AddFileRow(displayName, filePath);
        }

        dropToastCoroutine = null;
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
        }

        if (codeViewer != null)
        {
            codeViewer.UnregisterCallback<PointerDownEvent>(
                OnCodeViewerPointerDown
            );

            codeViewer.UnregisterCallback<PointerMoveEvent>(
                OnCodeViewerPointerMove
            );

            codeViewer.UnregisterCallback<PointerUpEvent>(
                OnCodeViewerPointerUp
            );

            codeViewer.UnregisterCallback<PointerCaptureOutEvent>(
                OnCodeViewerPointerCaptureOut
            );
        }

        if (codeResizeHandle != null)
        {
            codeResizeHandle.UnregisterCallback<PointerDownEvent>(
                OnCodeResizeHandlePointerDown
            );

            codeResizeHandle.UnregisterCallback<PointerMoveEvent>(
                OnCodeResizeHandlePointerMove
            );

            codeResizeHandle.UnregisterCallback<PointerUpEvent>(
                OnCodeResizeHandlePointerUp
            );

            codeResizeHandle.UnregisterCallback<PointerCaptureOutEvent>(
                OnCodeResizeHandlePointerCaptureOut
            );
        }

        if (filesWindow != null)
        {
            filesWindow.UnregisterCallback<PointerDownEvent>(
                OnFilesWindowPointerDown
            );

            filesWindow.UnregisterCallback<PointerMoveEvent>(
                OnFilesWindowPointerMove
            );

            filesWindow.UnregisterCallback<PointerUpEvent>(
                OnFilesWindowPointerUp
            );

            filesWindow.UnregisterCallback<PointerCaptureOutEvent>(
                OnFilesWindowPointerCaptureOut
            );
        }

        if (filesResizeHandle != null)
        {
            filesResizeHandle.UnregisterCallback<PointerDownEvent>(
                OnFilesResizeHandlePointerDown
            );

            filesResizeHandle.UnregisterCallback<PointerMoveEvent>(
                OnFilesResizeHandlePointerMove
            );

            filesResizeHandle.UnregisterCallback<PointerUpEvent>(
                OnFilesResizeHandlePointerUp
            );

            filesResizeHandle.UnregisterCallback<PointerCaptureOutEvent>(
                OnFilesResizeHandlePointerCaptureOut
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