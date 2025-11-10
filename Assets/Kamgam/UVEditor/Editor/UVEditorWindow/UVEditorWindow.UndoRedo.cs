using UnityEditor;
using UnityEditor.UIElements;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Collections;

namespace Kamgam.UVEditor
{
    // Undo / Redo Commands

    public partial class UVEditorWindow : EditorWindow
    {
        private void undo()
        {
            UndoStack.Instance.Undo();

            _uvsAreDirty = true;
            SetSelectedObject(_selectedObject);
            updateSelectionRect();
        }

        private void redo()
        {
            UndoStack.Instance.Redo();

            _uvsAreDirty = true;
            SetSelectedObject(_selectedObject); 
            updateSelectionRect();
        }

        private System.Action undoSetSelectedVerticesFunc()
        {
            // Make a copy
            var selectedVertices = new List<int>(_selectedVertices);

            // Apply
            return () => {
                _selectedVertices.Clear();
                _selectedVertices.AddRange(selectedVertices);
                onSelectedVerticesChanged();
            };
        }

        private System.Action undoSetUVsWorkingCopyFunc()
        {
            // Make copy
            var obj = _selectedObject;
            var uvChannel = _uvChannel;
            var uvs = new List<Vector2>(getUVsWorkingCopy(obj, uvChannel, _selectedMesh));

            // Apply
            return () => {
                setUVsWorkingCopy(obj, uvChannel, uvs);
            };
        }

        private System.Action undoAssignMeshOnGameObjectFunc(GameObject go, Mesh mesh)
        {
            // Make copy
            var obj = go;
            var meshPath = AssetDatabase.GetAssetPath(mesh);
            var meshIsUVCreatedCopy = meshPath.Contains(UVEditorCopyMarker);
            var meshCopy = Mesh.Instantiate(mesh);
            var meshReference = mesh;

            // Apply
            return () => {
                // If the mesh is just a reference to a pre-existing user made mesh then assign that to the renderer.
                if (!meshIsUVCreatedCopy)
                {
                    SetSharedMeshOnGameObject(obj, meshReference);
                }
                else
                {
                    AssetDatabase.DeleteAsset(meshPath);
                    AssetDatabase.CreateAsset(Mesh.Instantiate(meshCopy), meshPath);
                    var finalMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                    SetSharedMeshOnGameObject(obj, finalMesh);
                }
            };
        }

        private System.Action undoAssignNewMeshOnGameObjectFunc(GameObject go, UnityEngine.Object obj)
        {
            // Make copy
            var goCopy = go;
            var objCopy = obj;

            // Apply
            return () => {
                var newMesh = getNewMesh(objCopy, GetSharedMeshFromGameObject(go), recordUndo: false);
                SetSharedMeshOnGameObject(goCopy, newMesh);
            };
        }

        private System.Action undoDestroyMeshAssetFunc(Mesh newMesh)
        {
            // Make copy
            var path = AssetDatabase.GetAssetPath(newMesh);

            // Apply
            return () => {
                if (!string.IsNullOrEmpty(path))
                {
                    AssetDatabase.DeleteAsset(path);
                }
            };
        }

        private System.Action undoCreateMeshAssetFunc(Mesh newMesh)
        {
            // Make copy
            var path = AssetDatabase.GetAssetPath(newMesh);
            var mesh = Mesh.Instantiate(newMesh);

            // Apply
            return () => {
                if (!string.IsNullOrEmpty(path))
                {
                    AssetDatabase.CreateAsset(Mesh.Instantiate(mesh), path);
                }
            };
        }

        private System.Action undoRemoveFromNewMeshCacheFunc(UnityEngine.Object obj)
        {
            var cacheKey = obj;

            // Apply
            return () => {
                _newMeshes.Remove(cacheKey);
            };
        }

        private System.Action undoSetUVsOnGameObjectFunc(GameObject go, List<Vector2> uvs, int uvChannel)
        {
            // Make copy
            var goRef = go;
            var meshCopy = GetSharedMeshFromGameObject(goRef);
            var uvChannelCopy = uvChannel;
            var uvsCopy = new List<Vector2>(uvs);

            // Apply
            return () => {
                if (meshCopy == null)
                    meshCopy = GetSharedMeshFromGameObject(goRef);
                meshCopy.SetUVs(uvChannelCopy, uvsCopy);
            };
        }

        private System.Action undoDestroyTextureAssetFunc(Texture2D newTexture)
        {
            // Make copy
            var path = AssetDatabase.GetAssetPath(newTexture);

            // Apply
            return () => {
                if (!string.IsNullOrEmpty(path))
                {
                    AssetDatabase.DeleteAsset(path);
                }
            };
        }

        private System.Action undoCreateTextureAssetFunc(Texture2D newTexture)
        {
            // Make copy
            var path = AssetDatabase.GetAssetPath(newTexture);
            var importerInfo = TextureUtils.GetImporterInfo(newTexture);
            var texture = TextureUtils.CopyTexture(newTexture, makeUncompressed: true);

            // Apply
            return () => {
                if (!string.IsNullOrEmpty(path))
                {
                    TextureUtils.SaveTextureAsPNG(texture, path);
                    TextureUtils.ApplyTextureImporterInfo(texture, importerInfo);
                    AssetDatabase.ImportAsset(path);
                }
            };
        }

        private System.Action undoDestroyMaterialAssetFunc(Material newMaterial)
        {
            // Make copy
            var path = AssetDatabase.GetAssetPath(newMaterial);

            // Apply
            return () => {
                if (!string.IsNullOrEmpty(path))
                {
                    AssetDatabase.DeleteAsset(path);
                }
            };
        }

        private System.Action undoCreateMaterialAssetFunc(Material newMaterial)
        {
            // Make copy
            var path = AssetDatabase.GetAssetPath(newMaterial);
            var material = new Material(newMaterial);

            // Apply
            return () => {
                if (!string.IsNullOrEmpty(path))
                {
                    AssetDatabase.CreateAsset(new Material(material), path);
                }
            };
        }

        /// <summary>
        /// Stores a copy of the texture and material of the current submesh in the Undo stack.<br />
        /// Used by both texture-move and texture-crop for undo/redo of texture changes.
        /// </summary>
        private System.Action undoSetTextureAndMaterialsOnGameObjectFunc(GameObject go, Texture2D texture, Material material)
        {
            // Copy
            var renderer = GetRendererFromGameObject(go);

            var textureReference = texture;
            var texturePath = texture == null ? "" : AssetDatabase.GetAssetPath(texture);
            var textureIsUVCreatedCopy = texturePath.Contains(UVEditorCopyMarker);
            var textureImporterInfo = TextureUtils.GetImporterInfo(texture);
            var textureCopy = TextureUtils.CopyTexture(texture, makeUncompressed: true);

            var materialReference = material;
            var materialPath = material == null ? "" : AssetDatabase.GetAssetPath(material);
            var materialIsUVCreatedCopy = materialPath.Contains(UVEditorCopyMarker);
            var materialCopy = new Material(material);

            var materials = renderer.sharedMaterials;
            var materialIndices = new List<int>();
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] == material)
                {
                    materialIndices.Add(i);
                }
            }

            // Apply
            return () =>
            {
                // Set Material
                if (materialIsUVCreatedCopy)
                {
                    // If the material was generated by the UVEditor then re-use that material.
                    var loadedMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                    if (loadedMaterial != null)
                    {
                        loadedMaterial.CopyPropertiesFromMaterial(materialCopy);
                    }
                    else
                    {
                        // Recreate material if missing.
                        var newMaterial = new Material(materialCopy);
                        AssetDatabase.CreateAsset(newMaterial, materialPath);
                        loadedMaterial = newMaterial;
                    }

                    // Assign material to sub mesh slots.
                    if (renderer != null)
                    {
                        var tmpMaterials = renderer.sharedMaterials;
                        for (int i = 0; i < materialIndices.Count; i++)
                        {
                            tmpMaterials[materialIndices[i]] = loadedMaterial;
                        }
                        renderer.sharedMaterials = tmpMaterials;
                    }

                    // Set Texture (it's important this comes AFTER the material change because it assigns a new texture to the material).
                    if (textureIsUVCreatedCopy)
                    {
                        // If the texture is a copy then make another copy and use that on the material.

                        TextureImporter importer = TextureUtils.GetImporter(texturePath);

                        var copy = TextureUtils.CopyTexture(textureCopy, makeUncompressed: true);
                        TextureUtils.SaveTextureAsPNG(copy, texturePath);

                        if (importer != null)
                            importer.SaveAndReimport();
                        else
                            AssetDatabase.ImportAsset(texturePath);

                        var finalTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                        if (renderer != null && finalTexture != null)
                        {
                            TextureUtils.ApplyTextureImporterInfo(finalTexture, textureImporterInfo);
                            loadedMaterial.mainTexture = finalTexture;
                        }
                    }
                    else
                    {
                        // If the texture is just a reference to a pre-existing user made texture then assign that to the material.
                        if (renderer != null)
                        {
                            loadedMaterial.mainTexture = textureReference;
                        }
                    }
                }
                else
                {
                    // If the material is just a reference to a pre-existing material then assign that to the renderer.
                    if (renderer != null)
                    {
                        var tmpMaterials = renderer.sharedMaterials;
                        for (int i = 0; i < materialIndices.Count; i++)
                        {
                            tmpMaterials[materialIndices[i]] = materialReference;
                        }
                        renderer.sharedMaterials = tmpMaterials;
                    }
                }
            };
        }
    }
}