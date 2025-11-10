using UnityEditor;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

namespace Kamgam.UVEditor
{
    static class AssetPathUtils
    {
        /// <summary>
        /// Ensures that the directories in the specified path exist, and generates a unique asset path.
        /// </summary>
        /// <param name="filePath">The desired file path, including the file name.</param>
        /// <returns>A unique file path with necessary directories created.</returns>
        public static string GenerateUniqueAssetPathWithFolders(string filePath)
        {
            // Extract the folder path from the file path
            string folderPath = Path.GetDirectoryName(filePath);
            if (string.IsNullOrEmpty(folderPath))
            {
                Debug.LogError("Invalid path. Cannot extract folder path.");
                return null;
            }

            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                createFoldersRecursively(folderPath);
            }

            string uniquePath = AssetDatabase.GenerateUniqueAssetPath(filePath);
            return uniquePath;
        }

        private static void createFoldersRecursively(string folderPath)
        {
            folderPath = folderPath.Replace("\\", "/");
            string[] splitPath = folderPath.Split('/');

            string currentPath = splitPath[0];

            for (int i = 1; i < splitPath.Length; i++)
            {
                string nextFolder = currentPath + "/" + splitPath[i];

                if (!AssetDatabase.IsValidFolder(nextFolder))
                {
                    AssetDatabase.CreateFolder(currentPath, splitPath[i]);
                }

                currentPath = nextFolder;
            }
        }
    }
}
