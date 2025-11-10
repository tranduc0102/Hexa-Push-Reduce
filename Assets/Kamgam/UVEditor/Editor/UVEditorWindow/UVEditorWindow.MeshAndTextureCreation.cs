using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using Kamgam.UVEditor;
using System;
using System.Collections.Generic;
using System.IO;


namespace Kamgam.UVEditor
{
    public partial class UVEditorWindow : EditorWindow
    {
        private void applyUVChangesToSelectedObjectMesh(bool recordUndo, int uvChannel)
        {
            var uvWorkingCopy = getUVWorkingCopyForSelection(uvChannel);
            Mesh sharedMesh = GetSharedMeshFromGameObject(SelectedGameObject);

            // Update or create new mesh.
            var meshCopy = getNewMesh(_selectedObject, sharedMesh, recordUndo);
            if (meshCopy != null)
            {
                // Record undo
                if (recordUndo)
                {
                    UndoStack.Instance.StartEntry();
                    var currentUVs = new List<Vector2>();
                    sharedMesh.GetUVs(_uvChannel, currentUVs);
                    UndoStack.Instance.AddUndoAction(undoSetUVsOnGameObjectFunc(SelectedGameObject, currentUVs, _uvChannel));
                    UndoStack.Instance.AddUndoAction(undoAssignMeshOnGameObjectFunc(SelectedGameObject, sharedMesh));
                }

                // Update UVs
                meshCopy.SetUVs(uvChannel, uvWorkingCopy);
                SetSharedMeshOnGameObject(SelectedGameObject, meshCopy);

                if (recordUndo)
                {
                    UndoStack.Instance.AddRedoAction(undoAssignNewMeshOnGameObjectFunc(SelectedGameObject, _selectedObject));
                    UndoStack.Instance.AddRedoAction(undoSetUVsOnGameObjectFunc(SelectedGameObject, uvWorkingCopy, _uvChannel));
                    UndoStack.Instance.EndEntry();
                }
            }
        }

        /// <summary>
        /// Creates a copy of the current texture but also tries to find the old texture in case the
        /// given current texture is already a copy.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="subMeshIndex"></param>
        /// <param name="currentTexture"></param>
        /// <param name="recordUndo"></param>
        /// <returns></returns>
        protected Texture2D getNewTexture(UnityEngine.Object obj, int subMeshIndex, Texture2D currentTexture, bool recordUndo)
        {
            var key = new Tuple<UnityEngine.Object, int>(obj, subMeshIndex);

            if (_newTextures.ContainsKey(key))
            {
                if (_newTextures[key] == null || AssetDatabase.GetAssetPath(_newTextures[key]) == null)
                    _newTextures.Remove(key);
            }

            if (_newTextures.ContainsKey(key))
            {
                // Return cached texture.
                return _newTextures[key];
            }
            else
            {
                if (currentTexture != null)
                {
                    // Find texture or duplicate texture. 
                    string currentPath = AssetDatabase.GetAssetPath(currentTexture);
                    string currentFileName = Path.GetFileNameWithoutExtension(currentPath);
                    string extension = Path.GetExtension(currentPath);
                    // N2H/TODO: Hand over path and perfer the texture in the same path if multiple textures with
                    //           the same name have been found.
                    Texture2D newTexture = null;
                    if (currentFileName.Contains(UVEditorCopyMarker))
                    {
                        newTexture = findAndLoadAsset<Texture2D>("Texture", currentFileName);

                        // old texture
                        string oldFileName = currentFileName.Replace(UVEditorCopyMarker, "");
                    }

                    // If no texture was found then duplicate the current texture.
                    if (newTexture == null)
                    {
                        newTexture = duplicateTexture(currentTexture, recordUndo, subMeshIndex);
                        if (newTexture != null)
                        {
                            _newTextures.Add(key, newTexture);
                            EditorGUIUtility.PingObject(newTexture);
                        }
                    }
                    else if(newTexture != null)
                    {
                        _newTextures.Add(key, newTexture);
                    }

                    return newTexture;
                }

                return null;
            }
        }

        protected T findAndLoadAsset<T>(string type, string fileName) where T : UnityEngine.Object
        {
            var guids = AssetDatabase.FindAssets($"t:{type} {fileName}");
            if (guids.Length > 0)
            {
                foreach (var guid in guids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var name = Path.GetFileNameWithoutExtension(path);
                    if (name == fileName)
                        return AssetDatabase.LoadAssetAtPath<T>(path);
                }

                var firstPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                return AssetDatabase.LoadAssetAtPath<T>(firstPath);
            }

            return null;
        }

        protected Texture2D duplicateTexture(Texture2D originalTexture, bool recordUndo, int undoSubMeshIndex = -1)
        {
            string originalPath = AssetDatabase.GetAssetPath(originalTexture);

            if (string.IsNullOrEmpty(originalPath))
            {
                Logger.LogError("Could not find the asset path for the provided texture.");
                return null;
            }

            var settings = UVEditorSettings.GetOrCreateSettings();

            // Get the path infos of the original
            string folderPath = settings.ExtractedFilesLocation; // Path.GetDirectoryName(originalPath) + "/";
            string originalFileName = Path.GetFileNameWithoutExtension(originalPath);
            string extension = ".png"; // We store texture copies always in png format.

            // Create a new unique path for the duplicated texture
            string duplicatedPath = AssetPathUtils.GenerateUniqueAssetPathWithFolders($"Assets/{folderPath}{originalFileName}{UVEditorCopyMarker}{extension}");

            byte[] textureBytes = null;
            // Convert texture to png (make sure the source texture is readable), see:
            // https://discussions.unity.com/t/easy-way-to-make-texture-isreadable-true-by-script/848617/6
            TextureImporter ti = null;
            bool isReadable = originalTexture.isReadable;
            TextureImporterCompression compression = TextureImporterCompression.Compressed;
            try
            {
                // Ensure original texture is readable
                if (!isReadable)
                {
                    var origTexPath = AssetDatabase.GetAssetPath(originalTexture);
                    ti = (TextureImporter)AssetImporter.GetAtPath(origTexPath);
                    compression = ti.textureCompression;
                    ti.isReadable = true;
                    ti.textureCompression = TextureImporterCompression.Uncompressed;
                    ti.SaveAndReimport();
                }

                textureBytes = originalTexture.EncodeToPNG();
            }
            finally
            {
                // Revert
                if (!isReadable && ti != null)
                {
                    ti.isReadable = false;
                    ti.textureCompression = compression;
                    ti.SaveAndReimport();
                }
            }
            File.WriteAllBytes(duplicatedPath, textureBytes);

            AssetDatabase.ImportAsset(duplicatedPath);
            AssetDatabase.Refresh();

            // Load the duplicated texture into a new Texture2D variable
            Texture2D duplicatedTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(duplicatedPath);

            if (duplicatedTexture == null)
            {
                Logger.LogError("Failed to duplicate texture.");
                return null;
            }

            // Undo
            if (recordUndo)
            {
                UndoStack.Instance.StartEntry();
                UndoStack.Instance.AddRedoAction(undoCreateTextureAssetFunc(duplicatedTexture));
                UndoStack.Instance.AddUndoAction(undoDestroyTextureAssetFunc(duplicatedTexture));
                UndoStack.Instance.EndEntry();
            }

            return duplicatedTexture;
        }

        protected Material getNewMaterial(UnityEngine.Object obj, int subMeshIndex, Material currentMaterial, bool recordCreationUndo)
        {
            var key = new Tuple<UnityEngine.Object, int>(obj, subMeshIndex);

            if (_newMaterials.ContainsKey(key))
            {
                if (_newMaterials[key] == null || AssetDatabase.GetAssetPath(_newMaterials[key]) == null)
                    _newMaterials.Remove(key);
            }

            if (_newMaterials.ContainsKey(key))
            {
                // Return cached texture.
                return _newMaterials[key];
            }
            else
            {
                if (currentMaterial != null)
                {
                    // Find texture or duplicate texture. 
                    string currentPath = AssetDatabase.GetAssetPath(currentMaterial);
                    string currentFileName = Path.GetFileNameWithoutExtension(currentPath);
                    string extension = Path.GetExtension(currentPath);
                    // N2H/TODO: Hand over path and perfer the material in the same path if multiple materials with
                    //           the same name have been found.
                    Material newMaterial = null;
                    if (currentFileName.Contains(UVEditorCopyMarker))
                    {
                        newMaterial = findAndLoadAsset<Material>("Material", currentFileName);

                        // old texture
                        string oldFileName = currentFileName.Replace(UVEditorCopyMarker, "");
                    }

                    // If no texture was found then duplicate the current texture.
                    if (newMaterial == null)
                    {
                        newMaterial = duplicateMaterial(currentMaterial, recordCreationUndo, subMeshIndex);
                        if (newMaterial != null)
                        {
                            _newMaterials.Add(key, newMaterial);
                            EditorGUIUtility.PingObject(newMaterial);
                        }
                    }
                    else if (newMaterial != null)
                    {
                        _newMaterials.Add(key, newMaterial);
                    }

                    return newMaterial;
                }

                return null;
            }
        }

        protected Material duplicateMaterial(Material originalMaterial, bool recordUndo, int undoSubMeshIndex = -1)
        {
            string originalPath = AssetDatabase.GetAssetPath(originalMaterial);

            if (string.IsNullOrEmpty(originalPath))
            {
                Logger.LogError("Could not find the asset path for the provided texture.");
                return null;
            }

            var settings = UVEditorSettings.GetOrCreateSettings();

            // Get the path infos of the original
            string folderPath = settings.ExtractedFilesLocation; // Path.GetDirectoryName(originalPath) + "/";
            string originalFileName = Path.GetFileNameWithoutExtension(originalPath);
            string extension = Path.GetExtension(originalPath);

            // Create a new unique path for the duplicated texture
            string duplicatedPath = AssetPathUtils.GenerateUniqueAssetPathWithFolders($"Assets/{folderPath}{originalFileName}{UVEditorCopyMarker}{extension}");

            var duplicatedMaterial = new Material(originalMaterial);
            AssetDatabase.CreateAsset(duplicatedMaterial, duplicatedPath);

            // Undo
            if (recordUndo)
            {
                UndoStack.Instance.StartEntry();
                UndoStack.Instance.AddRedoAction(undoCreateMaterialAssetFunc(duplicatedMaterial));
                UndoStack.Instance.AddUndoAction(undoDestroyMaterialAssetFunc(duplicatedMaterial));
                UndoStack.Instance.EndEntry();
            }

            return duplicatedMaterial;
        }

        /// <summary>
        /// If the mesh is a mesh that was NOT generated by the UV Editor tool (which is 
        /// determined by the file name ending) then it will create a COPY if the mesh
        /// and return that. If the mesh is a UV Editor tool generated one then it will
        /// return that mesh. It does cache the results to improve performance.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="oldMesh"></param>
        /// <param name="recordUndo"></param>
        /// <param name="clearCache">Clears the cache for the given object.</param>
        /// <returns></returns>
        protected Mesh getNewMesh(UnityEngine.Object obj, Mesh oldMesh, bool recordUndo, bool clearCache = false)
        {
            if (clearCache)
                _newMeshes.Remove(obj);

            if(_newMeshes.ContainsKey(obj))
            {
                if (_newMeshes[obj] == null || AssetDatabase.GetAssetPath(_newMeshes[obj]) == null)
                    _newMeshes.Remove(obj);
            }

            if (_newMeshes.ContainsKey(obj))
            {
                return _newMeshes[obj];
            }
            else
            {
                if (oldMesh != null)
                {
                    // Find mesh or duplicate mesh. 
                    Mesh newMesh = null;
                    string oldPath = AssetDatabase.GetAssetPath(oldMesh);
                    if (!string.IsNullOrEmpty(oldPath))
                    {
                        string oldFileName = Path.GetFileNameWithoutExtension(oldPath);
                        string extension = Path.GetExtension(oldPath);
                        // TODO/N2H: Hand over path and perfer the mesh in the same path if multiple meshes with the same name have been found.
                        if (oldFileName.Contains(UVEditorCopyMarker))
                        {
                            newMesh = findAndLoadAsset<Mesh>("Mesh", oldFileName);
                        }
                    }

                    // If no mesh was found then duplicate the old one.
                    if (newMesh == null)
                    {
                        newMesh = duplicateMesh(oldMesh, recordUndo);
                        if (newMesh != null)
                        {
                            _newMeshes.Add(obj, newMesh);
                            return newMesh;
                        }
                    }
                    else if (newMesh != null)
                    {
                        _newMeshes.Add(obj, newMesh);
                        return newMesh; 
                    }
                }

                if(recordUndo)
                {
                    UndoStack.Instance.StartEntry();
                    UndoStack.Instance.AddUndoAction(undoRemoveFromNewMeshCacheFunc(_selectedObject));
                    UndoStack.Instance.AddRedoAction(undoRemoveFromNewMeshCacheFunc(_selectedObject));
                    UndoStack.Instance.StartEntry();
                }

                return null;
            }
        }

        protected Mesh duplicateMesh(Mesh originalMesh, bool recordUndo)
        {
            var settings = UVEditorSettings.GetOrCreateSettings();
            string originalPath = AssetDatabase.GetAssetPath(originalMesh);

            // Get the path infos of the original
            string folderPath = settings.ExtractedFilesLocation; // Path.GetDirectoryName(originalPath) + "/";
            string originalFileName;
            string extension = ".asset";
            if (string.IsNullOrEmpty(originalPath))
            {
                // The mesh is probably stored in the scene or is a dynamic mesh.
                // Let's create the copy in the default location.
                // If the mesh has no name then we assign one randomly.
                originalFileName = string.IsNullOrEmpty(originalMesh.name) ? "Mesh" + UnityEngine.Random.Range(1,9_999_999) : originalMesh.name;
            }
            else
            {
                originalFileName = Path.GetFileNameWithoutExtension(originalPath);
            }

            // Create a new unique path for the duplicated mesh
            string duplicatedPath = AssetPathUtils.GenerateUniqueAssetPathWithFolders($"Assets/{folderPath}{originalFileName}{UVEditorCopyMarker}{extension}");

            // Create asset
            Mesh duplicatedMesh = Mesh.Instantiate(originalMesh);
            if (duplicatedMesh != null)
            {
                AssetDatabase.CreateAsset(duplicatedMesh, duplicatedPath);

                // Undo
                if (recordUndo)
                {
                    UndoStack.Instance.StartEntry();
                    UndoStack.Instance.AddRedoAction(undoCreateMeshAssetFunc(duplicatedMesh));
                    UndoStack.Instance.AddUndoAction(undoDestroyMeshAssetFunc(duplicatedMesh));
                    UndoStack.Instance.EndEntry();
                }
            }
            else
            {
                Logger.LogError("Failed to duplicate mesh.");
                return null;
            }

            return duplicatedMesh;
        }

        public static Renderer GetRendererFromGameObject(GameObject go)
        {
            var meshRenderer = go.GetComponentInChildren<MeshRenderer>();
            if (meshRenderer)
            {
                return meshRenderer;
            }
            else
            {
                var skinnedMeshRenderer = go.GetComponentInChildren<SkinnedMeshRenderer>();
                if (skinnedMeshRenderer)
                    return skinnedMeshRenderer;
                else
                    return null;
            }
        }

        public static Mesh GetSharedMeshFromGameObject(GameObject go)
        {
            var meshFilter = go.GetComponentInChildren<MeshFilter>();
            if (meshFilter)
            {
                return meshFilter.sharedMesh;
            }
            else
            {
                var skinnedMeshRenderer = go.GetComponentInChildren<SkinnedMeshRenderer>();
                if (skinnedMeshRenderer)
                    return skinnedMeshRenderer.sharedMesh;
                else
                    return null;
            }
        }

        public static void SetSharedMeshOnGameObject(GameObject go, Mesh mesh)
        {
            var meshFilter = go.GetComponentInChildren<MeshFilter>();
            if (meshFilter)
            {
                meshFilter.sharedMesh = mesh;
            }
            else
            {
                var skinnedMeshRenderer = go.GetComponentInChildren<SkinnedMeshRenderer>();
                if (skinnedMeshRenderer)
                    skinnedMeshRenderer.sharedMesh = mesh;
            }
        }
    }
}