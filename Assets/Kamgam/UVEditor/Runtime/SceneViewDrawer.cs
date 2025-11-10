using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Kamgam.UVEditor
{
#if !UNITY_EDITOR
    public class SceneViewDrawer : MonoBehaviour {}
#else

    /// <summary>
    /// Used to enable GL.* drawing method in BuiltIn and URP/HDRP
    /// render pipelines.
    /// </summary>
    [ExecuteInEditMode]
    public class SceneViewDrawer : MonoBehaviour, ISerializationCallbackReceiver
    {
        const string Name = "[Temp] UVEditor.SceneViewDrawer";

        // Singleton
        ////////////////////////////////////////////
        static SceneViewDrawer _instance;

        public static SceneViewDrawer Instance()
        {
            if (_instance != null && !_instance.Destroyed)
            {
                return _instance;
            }

            var go = new GameObject(Name);
            go.hideFlags = HideFlags.HideAndDontSave;
            var drawer = go.AddComponent<SceneViewDrawer>();
            _instance = drawer;
            _instance.Destroyed = false;

            return drawer;
        }



        // Render Event (the actual purpose of this class)
        ////////////////////////////////////////////
        public event System.Action OnRender;

        void OnRenderObject()
        {
            if (Destroyed)
                return;

            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            OnRender?.Invoke();
        }



        // Self-Destruct after deserialization
        ////////////////////////////////////////////
        
        [System.NonSerialized]
        public bool Destroyed;

        public void OnBeforeSerialize()
        {
        }

        public void OnAfterDeserialize()
        {
            // Destroy self
            Destroyed = true;
            _instance = null;

            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null && gameObject != null)
                {
                    GameObject.DestroyImmediate(gameObject);
                }
            };
        }
    }
#endif
}


