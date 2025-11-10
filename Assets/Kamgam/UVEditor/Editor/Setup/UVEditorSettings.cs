#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.Compilation;
using UnityEditor.SceneManagement;
using UnityEditor.ShortcutManagement;
using UnityEngine;

namespace Kamgam.UVEditor
{
    // Create a new type of Settings Asset.
    public class UVEditorSettings : ScriptableObject
    {
        public const string Version = "1.0.0";
        public const string SettingsFilePath = "Assets/UVEditorSettings.asset";

        [SerializeField, Tooltip(_logLevelTooltip)]
        public Logger.LogLevel LogLevel;
        public const string _logLevelTooltip = "Any log above this log level will not be shown. To turn off all logs choose 'NoLogs'";

        [SerializeField, Tooltip(_doNotCropTextureNamesTooltip)]
        public string[] DoNotCropTextureNames;
        public const string _doNotCropTextureNamesTooltip = "Defines shader texture property name exceptions for texture cropping. All texture property names set here will never be cropped.";

        [SerializeField, Tooltip(_scrollWheelSensitivityTooltip)]
        public float ScrollWheelSensitivity;
        public const string _scrollWheelSensitivityTooltip = "The sensitivity of the scroll wheel (determines how fast it will change the brush size).";

        [SerializeField, Tooltip(_extractedFilesLocationTooltip)]
        public string ExtractedFilesLocation;
        public const string _extractedFilesLocationTooltip = "The location (relative to Assets/) where the extracted files are stored. This string is appeneded to the file name you choose in the extraction dialog.";
        
        [SerializeField, Tooltip(_logFilePathsTooltip)]
        public bool LogFilePaths;
        public const string _logFilePathsTooltip = "Should the created file paths be logged in the console?";

        [SerializeField, Tooltip(_disableOnHierarchyChangeTooltip)]
        public bool DisableOnHierarchyChange;
        public const string _disableOnHierarchyChangeTooltip = "Disable the tool if the hierarchy changes (convenience). Disable if it annoys you.";

        [SerializeField, Tooltip(_triggerSelectLinkedTooltip)]
        public KeyCode TriggerSelectLinked;
        public const string _triggerSelectLinkedTooltip = "Pressing this key while in 'Select Polygons' mode triggers the 'Select Linked' action.";

        [SerializeField, Tooltip(_maxSelectLinkedDuration)]
        public int MaxSelectLinkedDuration = 10;
        public const string _maxSelectLinkedDuration = "Select linked may take a long time for highpoly meshes. This limits the time it is allowed to take (in seconds).";

        [SerializeField, Tooltip(_showSelectLinkedFailedPopup)]
        public bool ShowSelectLinkedFailedPopup = true;
        public const string _showSelectLinkedFailedPopup = "Should there be a popup window if select linked failed? If turned off then a log message will be used instead.";

        [Range(0,1)]
        public float SelectionColorAlpha;

        public Color[] SelectionColorSubMeshes;

        [SerializeField, Tooltip(_warnAboutOldSelectionsTooltip)]
        public bool WarnAboutOldSelections;
        public const string _warnAboutOldSelectionsTooltip = "Show a warning dialog if an old selection exists?";

        [SerializeField, Tooltip(_clearOldSelectionsAutomaticallyTooltip)]
        public bool ClearOldSelectionsAutomatically;
        public const string _clearOldSelectionsAutomaticallyTooltip = "Clear old selections automatically? If enabled then the WarnAboutOldSelections will have no effect since they are cleared automatically.";

        public bool AskBeforeReplacingExistingSelectionSnapshot = true;
        public bool ShowSelectedObjectPositionWarning = true;

        public KeyCode RedoKey = KeyCode.Y; // Fallback: Updated by UpdateKeysFromBindings();
        public KeyCode UndoKey = KeyCode.Z; // Fallback: Updated by UpdateKeysFromBindings();

        public Vector2 WindowPosition;

        public List<SelectedTriangleSnapshot> SelectionSnapshots = new List<SelectedTriangleSnapshot>();

        [RuntimeInitializeOnLoadMethod]
        static void bindLoggerLevelToSetting()
        {
            // Notice: This does not yet create a setting instance!
            Logger.OnGetLogLevel = () => GetOrCreateSettings().LogLevel;
        }

        [InitializeOnLoadMethod]
        static void autoCreateSettings()
        {
            GetOrCreateSettings();
        }

        static UVEditorSettings cachedSettings;

        public static UVEditorSettings GetOrCreateSettings()
        {
            if (cachedSettings == null)
            {
                string typeName = typeof(UVEditorSettings).Name;

                cachedSettings = AssetDatabase.LoadAssetAtPath<UVEditorSettings>(SettingsFilePath);

                // Still not found? Then search for it.
                if (cachedSettings == null)
                {
                    string[] results = AssetDatabase.FindAssets("t:" + typeName);
                    if (results.Length > 0)
                    {
                        string path = AssetDatabase.GUIDToAssetPath(results[0]);
                        cachedSettings = AssetDatabase.LoadAssetAtPath<UVEditorSettings>(path);
                    }
                }

                if (cachedSettings != null)
                {
                    SessionState.EraseBool(typeName + "WaitingForReload");
                }

                // Still not found? Then create settings.
                if (cachedSettings == null)
                {
                    CompilationPipeline.compilationStarted -= onCompilationStarted;
                    CompilationPipeline.compilationStarted += onCompilationStarted;

                    // Are the settings waiting for a recompile to finish? If yes then return null;
                    // This is important if an external script tries to access the settings before they
                    // are deserialized after a re-compile.
                    bool isWaitingForReloadAfterCompilation = SessionState.GetBool(typeName + "WaitingForReload", false);
                    if (isWaitingForReloadAfterCompilation)
                    {
                        Debug.LogWarning(typeName + " is waiting for assembly reload.");
                        return null;
                    }

                    cachedSettings = ScriptableObject.CreateInstance<UVEditorSettings>();
                    cachedSettings.LogLevel = Logger.LogLevel.Warning;
                    cachedSettings.DoNotCropTextureNames = new string[0];
                    cachedSettings.ScrollWheelSensitivity = 1f;
                    cachedSettings.WindowPosition = new Vector2(-1, -1);
                    cachedSettings.ExtractedFilesLocation = "UVEditorAssets/";
                    cachedSettings.LogFilePaths = false;
                    cachedSettings.DisableOnHierarchyChange = true;
                    cachedSettings.TriggerSelectLinked = KeyCode.S;
                    cachedSettings.MaxSelectLinkedDuration = 10;
                    cachedSettings.SelectionColorAlpha = 0.5f;
                    cachedSettings.SelectionColorSubMeshes = new Color[]
                    {
                        new Color(0f, 1f, 0f),
                        new Color(0f, 1f, 0.5f),
                        new Color(0f, 1f, 0.75f),
                        new Color(0.75f, 1f, 0f),
                        new Color(1f, 1f, 0f)
                    };
                    cachedSettings.ShowSelectLinkedFailedPopup = true;
                    cachedSettings.WarnAboutOldSelections = true;
                    cachedSettings.ClearOldSelectionsAutomatically = false;
                    cachedSettings.AskBeforeReplacingExistingSelectionSnapshot = true;
                    cachedSettings.ShowSelectedObjectPositionWarning = true;
                    cachedSettings.UndoKey = KeyCode.Z; // Fallback: Updated by UpdateKeysFromBindings();
                    cachedSettings.RedoKey = KeyCode.Y; // Fallback: Updated by UpdateKeysFromBindings();

                    // update from key bindings
                    cachedSettings.UpdateKeysFromBindings();
                    ShortcutManager.instance.shortcutBindingChanged -= cachedSettings.onKeyBindingsChanged;
                    ShortcutManager.instance.shortcutBindingChanged += cachedSettings.onKeyBindingsChanged;

                    AssetDatabase.CreateAsset(cachedSettings, SettingsFilePath);
                    AssetDatabase.SaveAssets();

                    onSettingsCreated();

                    Logger.OnGetLogLevel = () => cachedSettings.LogLevel;
                }
            }

            return cachedSettings;
        }

        private void onKeyBindingsChanged(ShortcutBindingChangedEventArgs change)
        {
            UpdateKeysFromBindings();
        }

        public void UpdateKeysFromBindings()
        {
            cachedSettings.UndoKey = TryGetKeyCodeForBinding("Menu/Edit/Undo", KeyCode.Z);
            cachedSettings.RedoKey = TryGetKeyCodeForBinding("Menu/Edit/Redo", KeyCode.Y);
        }

        static KeyCode TryGetKeyCodeForBinding(string binding, KeyCode defaultValue)
        {
            // https://docs.unity3d.com/2019.4/Documentation/ScriptReference/ShortcutManagement.IShortcutManager.GetAvailableShortcutIds.html
            // Throws ArgumentException if shortcutId is not available, i.e. when GetAvailableShortcutIds does not contain shortcutId.
            var bindings = ShortcutManager.instance.GetAvailableShortcutIds();
            foreach (var b in bindings)
            {
                if (b == binding)
                {
                    var combo = ShortcutManager.instance.GetShortcutBinding(binding).keyCombinationSequence;
                    foreach (var c in combo)
                    {
                        return c.keyCode;
                    }
                }
            }

            return defaultValue;
        }

        private static void onCompilationStarted(object obj)
        {
            string typeName = typeof(UVEditorSettings).Name;
            SessionState.SetBool(typeName + "WaitingForReload", true);
        }

        // We use this callback instead of CompilationPipeline.compilationFinished because
        // compilationFinished runs before the assemply has been reloaded but DidReloadScripts
        // runs after. And only after we can access the Settings asset.
        [UnityEditor.Callbacks.DidReloadScripts(999000)]
        public static void DidReloadScripts()
        {
            string typeName = typeof(UVEditorSettings).Name;
            SessionState.EraseBool(typeName + "WaitingForReload");
        }

        static void onSettingsCreated()
        {
            bool openManual = EditorUtility.DisplayDialog(
                    "UV Editor",
                    "Thank you for choosing UV Editor.\n\n" +
                    "You'll find the tool under Tools > UV Editor > Start\n\n" +
                    "Please start by reading the manual.\n\n" +
                    "It would be great if you could find the time to leave a review.",
                    "Open manual", "Cancel"
                    );

            if (openManual)
            {
                Installer.OpenManual();
            }
        }

        internal static SerializedObject GetSerializedSettings()
        {
            return new SerializedObject(GetOrCreateSettings());
        }

        [MenuItem("Tools/UV Editor/Settings", priority = 100)]
        public static void OpenSettings()
        {
            var settings = UVEditorSettings.GetOrCreateSettings();
            if (settings != null)
            {
                Selection.activeObject = settings;
                EditorGUIUtility.PingObject(settings);
            }
            else
            {
                EditorUtility.DisplayDialog("Error", "UV Editor Settings could not be found or created.", "Ok");
            }
        }

        public void Save()
        {
            EditorUtility.SetDirty(this);
            SaveAssetHelper.SaveAssetIfDirty(this);
        }

    }


#if UNITY_EDITOR
    [CustomEditor(typeof(UVEditorSettings))]
    public class UVEditorSettingsEditor : Editor
    {
        public UVEditorSettings settings;

        public void OnEnable()
        {
            settings = target as UVEditorSettings;
        }

        public override void OnInspectorGUI()
        {
            EditorGUILayout.LabelField("Version: " + UVEditorSettings.Version);
            base.OnInspectorGUI();
        }
    }
#endif

    static class UVEditorSettingsProvider
    {
        [SettingsProvider]
        public static UnityEditor.SettingsProvider CreateUVEditorSettingsProvider()
        {
            var provider = new UnityEditor.SettingsProvider("Project/UV Editor", SettingsScope.Project)
            {
                label = "UV Editor",
                guiHandler = (searchContext) =>
                {
                    var settings = UVEditorSettings.GetSerializedSettings();

                    var style = new GUIStyle(GUI.skin.label);
                    style.wordWrap = true;

                    EditorGUILayout.LabelField("Version: " + UVEditorSettings.Version);
                    if (drawButton(" Open Manual ", icon: "_Help"))
                    {
                        Installer.OpenManual();
                    }

                    drawField("LogLevel", "Log Level", UVEditorSettings._logLevelTooltip, settings, style);
                    drawField("DoNotCropTextureNames", "Do Not Crop Texture Names", UVEditorSettings._doNotCropTextureNamesTooltip, settings, style);
                    drawField("ScrollWheelSensitivity", "Scroll Wheel Sensitivity", UVEditorSettings._scrollWheelSensitivityTooltip, settings, style);
                    drawField("ExtractedFilesLocation", "Extracted Files Location", UVEditorSettings._extractedFilesLocationTooltip, settings, style);
                    drawField("LogFilePaths", "Log File Paths", UVEditorSettings._logFilePathsTooltip, settings, style);
                    drawField("DisableOnHierarchyChange", "Disable On Hierarchy Change", UVEditorSettings._disableOnHierarchyChangeTooltip, settings, style);
                    drawField("TriggerSelectLinked", "Trigger Select Linked", UVEditorSettings._triggerSelectLinkedTooltip, settings, style);
                    drawField("MaxSelectLinkedDuration", "Max Select Linked Duration", UVEditorSettings._maxSelectLinkedDuration, settings, style);
                    drawField("ShowSelectLinkedFailedPopup", "Show Select Linked Failed Popup", UVEditorSettings._showSelectLinkedFailedPopup, settings, style);
                    drawField("SelectionColorAlpha", "Selection Color Alpha", null, settings, style);
                    drawField("WarnAboutOldSelections", "Warn about old selections", UVEditorSettings._warnAboutOldSelectionsTooltip, settings, style);
                    drawField("ClearOldSelectionsAutomatically", "Clear Old Selections Automatically", UVEditorSettings._clearOldSelectionsAutomaticallyTooltip, settings, style);

                    settings.ApplyModifiedProperties();
                },

                // Populate the search keywords to enable smart search filtering and label highlighting.
                keywords = new System.Collections.Generic.HashSet<string>(new[] { "shader", "triplanar", "rendering" })
            };

            return provider;
        }

        static void drawField(string propertyName, string label, string tooltip, SerializedObject settings, GUIStyle style)
        {
            EditorGUILayout.PropertyField(settings.FindProperty(propertyName), new GUIContent(label));
            if (!string.IsNullOrEmpty(tooltip))
            {
                GUILayout.BeginVertical(EditorStyles.helpBox);
                GUILayout.Label(tooltip, style);
                GUILayout.EndVertical();
            }
            GUILayout.Space(10);
        }

        static bool drawButton(string text, string tooltip = null, string icon = null, params GUILayoutOption[] options)
        {
            GUIContent content;

            // icon
            if (!string.IsNullOrEmpty(icon))
                content = EditorGUIUtility.IconContent(icon);
            else
                content = new GUIContent();

            // text
            content.text = text;

            // tooltip
            if (!string.IsNullOrEmpty(tooltip))
                content.tooltip = tooltip;

            return GUILayout.Button(content, options);
        }
    }
}
#endif