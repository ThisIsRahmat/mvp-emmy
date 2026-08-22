using System;
using System.Runtime.InteropServices;
#if UNITY_STANDALONE_OSX
using AOT;
#endif

namespace Shibuya24.Utility
{
    public static class UniDragAndDrop
    {
        /// <summary>
        /// Callback to return the local path of a drag-and-drop file
        /// </summary>
        public static Action<string> onDragAndDropFilePath;

        /// <summary>
        /// Initialization functions that must be called
        /// </summary>
        public static void Initialize()
        {
#if UNITY_STANDALONE_OSX
            Initialize(cs_callback);
#endif
        }

        /// <summary>
        /// Arms the given file to be dragged out to the OS (Finder,
        /// another app) on the very next native mouse-drag. Call this
        /// on pointer-down over a draggable row; it only fires once,
        /// so call it again for each new drag attempt. Pass null (or
        /// don't call again) to disarm - e.g. on pointer-up if no
        /// drag actually happened.
        /// </summary>
        public static void ArmFileDragForNextDrag(string filePath)
        {
#if UNITY_STANDALONE_OSX
            ArmFileDragForNextDrag_Native(filePath);
#endif
        }

        delegate void callback_delegate(string val);

#if UNITY_STANDALONE_OSX
        [DllImport("UniDragAndDrop")]
        private static extern void Initialize(callback_delegate callback);

        [DllImport("UniDragAndDrop", EntryPoint = "ArmFileDragForNextDrag")]
        private static extern void ArmFileDragForNextDrag_Native(string filePath);

        // call from Objective-C
        [MonoPInvokeCallback(typeof(callback_delegate))]
        private static void cs_callback(string val)
        {
            onDragAndDropFilePath?.Invoke(val);
        }
#endif
    }
}
