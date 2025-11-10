using System;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;

namespace Kamgam.UVEditor
{
    [InitializeOnLoad]
    public static class UVTriangleSelectToolActiveState
    {
        static bool _initialized = false;
        public static bool IsActive = false;

        static UVTriangleSelectToolActiveState()
        {
            init();
        }

        static void init()
        {
            _initialized = true;

            bool active = false;

            // EditorTools available until Unity 2020.1 (2020.2+ does not longer have this class)
#if UNITY_2020_2_OR_NEWER
            ToolManager.activeToolChanged -= onToolChanged;
            ToolManager.activeToolChanged += onToolChanged;

            // active right from the start?
            if (ToolManager.activeToolType == typeof(UVTriangleSelectTool))
            {
                // Remember: UVEditorTool.Instance is still null here! 
                active = true;
                waitForInstance(active);
            }
#else
            EditorTools.activeToolChanged -= onToolChanged;
            EditorTools.activeToolChanged += onToolChanged;

            // active right from the start?
            if (EditorTools.activeToolType == typeof(UVTriangleSelectTool))
            {
                // Remember: UVEditorTool.Instance is still null here! 
                active = true;
                waitForInstance(active);
            }
#endif
        }

        static async void waitForInstance(bool active) 
        {
            float totalWaitTime = 0f; // precaution against endlessly running task
            while (UVTriangleSelectTool.Instance == null && totalWaitTime < 3000)
            {
                await System.Threading.Tasks.Task.Delay(50);
                totalWaitTime += 50;
            }

            if (totalWaitTime >= 3000)
                return;

            IsActive = active;
            UVTriangleSelectTool.Instance.OnToolChanged();
        }

        static void onToolChanged()
        {
#if UNITY_2020_2_OR_NEWER
            IsActive = ToolManager.activeToolType == typeof(UVTriangleSelectTool);
#else
            IsActive = EditorTools.activeToolType == typeof(UVTriangleSelectTool);
#endif

            if (UVTriangleSelectTool.Instance != null)
                UVTriangleSelectTool.Instance.OnToolChanged();
        }

        [UnityEditor.Callbacks.DidReloadScripts]
        private static void OnScriptsReloaded()
        {
            if(!_initialized)
                init();
        }
    }
}
