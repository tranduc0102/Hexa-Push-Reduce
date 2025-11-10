using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Kamgam.UVEditor
{
    public static class TextureUtils
    {
        public class TextureImporterInfo
        {
            public bool success;

            public TextureImporter importer;

            public bool isReadable;
            public bool isCrunchCompressed;
            public TextureImporterType textureType;
            public TextureImporterCompression textureCompression;
            public int maxTextureSize;
        }

        /// <summary>
        /// Returns a TextureImporterInfo object which can be used to revert the changes made on 'texture'.
        /// </summary>
        /// <param name="texture"></param>
        /// <param name="makeUncompressed"></param>
        /// <returns></returns>
        public static TextureImporterInfo MakeTextureAssetReadableAndUncompressed(Texture2D texture, bool makeUncompressed = true)
        {
            var info = new TextureImporterInfo();
            info.success = false;

            try
            {
                var path = AssetDatabase.GetAssetPath(texture);
                info.importer = (TextureImporter)AssetImporter.GetAtPath(path);

                if (info.importer == null)
                {
                    Logger.LogError("TextureUtils: Importer is null, probably because the given texture is no asset.");
                    info.success = false;
                    return info;
                }

                // Record state
                info.isReadable = texture.isReadable;
                info.isCrunchCompressed = info.importer.crunchedCompression;
                info.textureCompression = info.importer.textureCompression;
                info.textureType = info.importer.textureType;
                info.maxTextureSize = info.importer.maxTextureSize;

                if (!texture.isReadable)
                {
                    info.importer.isReadable = true;
                }

                if (makeUncompressed)
                {
                    info.importer.crunchedCompression = false;
                    info.importer.textureCompression = TextureImporterCompression.Uncompressed;
                    info.importer.textureType = TextureImporterType.Default;
                    info.importer.maxTextureSize = 16384;
                }

                info.importer.SaveAndReimport();

                info.success = true;
            }
            catch
            {
                info.success = false;

                // Revert on error
                if (info.importer != null)
                {
                    info.importer.crunchedCompression = info.isCrunchCompressed;
                    info.importer.textureCompression = info.textureCompression;
                    info.importer.maxTextureSize = info.maxTextureSize;
                    info.importer.textureType = info.textureType;
                    info.importer.isReadable = info.isReadable;
                    info.importer.SaveAndReimport();
                }
            }

            return info;
        }

        public static TextureImporterInfo GetImporterInfo(Texture2D texture)
        {
            var info = new TextureImporterInfo();

            var path = AssetDatabase.GetAssetPath(texture);
            if (path != null)
            {
                info.importer = (TextureImporter)AssetImporter.GetAtPath(path);

                if (info.importer == null)
                {
                    Logger.LogError("TextureUtils: Importer is null, probably because the given texture is no asset.");
                    info.success = false;
                    return info;
                }

                // Record state
                info.isReadable = texture.isReadable;
                info.isCrunchCompressed = info.importer.crunchedCompression;
                info.textureCompression = info.importer.textureCompression;
                info.textureType = info.importer.textureType;
                info.maxTextureSize = info.importer.maxTextureSize;

                info.success = true;
                return info;
            }
            else
            {
                info.success = false;
                return info;
            }
        }

        public static TextureImporter GetImporter(Texture2D texture)
        {
            var path = AssetDatabase.GetAssetPath(texture);
            if (path != null)
                return (TextureImporter)AssetImporter.GetAtPath(path);
            else
                return null;
        }

        public static TextureImporter GetImporter(string path)
        {
            if (!string.IsNullOrEmpty(path))
                return (TextureImporter)AssetImporter.GetAtPath(path);
            else
                return null;
        }

        public static bool ApplyTextureImporterInfo(Texture2D textureAsset, TextureImporterInfo info)
        {
            if (info == null)
                return true;

            try
            {
                // If no valid importer exists then try to get one based on the texture.
                if (info.importer == null)
                {
                    var path = AssetDatabase.GetAssetPath(textureAsset);
                    if (path != null)
                    {
                        info.importer = (TextureImporter)AssetImporter.GetAtPath(path);
                    }
                }

                if (info.importer != null)
                {
                    info.importer.crunchedCompression = info.isCrunchCompressed;
                    info.importer.textureCompression = info.textureCompression;
                    info.importer.maxTextureSize = info.maxTextureSize;
                    info.importer.textureType = info.textureType;
                    info.importer.isReadable = info.isReadable;
                    info.importer.SaveAndReimport();
                    return true;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        public static Texture2D CopyTexture(Texture2D texture, bool makeUncompressed = false)
        {
            if (texture == null)
                return null;

            bool isAsset = !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(texture));

            bool reformatTexture = !texture.isReadable || (makeUncompressed && IsCompressed(texture));
            TextureImporterInfo info = null;
            if (reformatTexture && isAsset)
                info = MakeTextureAssetReadableAndUncompressed(texture, makeUncompressed);

            var  copy = new Texture2D(texture.width, texture.height, texture.format, texture.mipmapCount != 1);
            Graphics.CopyTexture(texture, copy);

            if (reformatTexture && info != null && info.success)
                ApplyTextureImporterInfo(texture, info);

            return copy;
        }

        public static (Color[], Vector2Int) GetPixels(Texture2D texture, Vector2 min, Vector2 max)
        {
            int width = Mathf.CeilToInt(max.x) - Mathf.FloorToInt(min.x);
            int height = Mathf.CeilToInt(max.y) - Mathf.FloorToInt(min.y);

            var pixelData = GetPixels(texture,
                Mathf.FloorToInt(min.x),
                Mathf.FloorToInt(min.y),
                width,
                height
                );

            return (pixelData, new Vector2Int(width, height));
        }

        public static Color[] GetPixels(Texture2D texture, Vector2Int min, Vector2Int max)
        {
            return GetPixels(texture, min.x, min.y, max.x - min.x, max.y - min.y);
        }

        public static Color[] GetPixels(Texture2D texture, int xMin, int yMin, int width, int height)
        {
            if (texture.isReadable)
            {
                return texture.GetPixels(xMin, yMin, width, height);
            }

            var info = MakeTextureAssetReadableAndUncompressed(texture);
            if (info.success)
            {
                var pixelData = texture.GetPixels(xMin, yMin, width, height);
                ApplyTextureImporterInfo(texture, info);
                return pixelData;
            }
            else
            {
                return null;
            }
        }

        public static void SetPixels(Texture2D texture, Color[] pixelData, Vector2 min, Vector2 max)
        {
            int width = Mathf.CeilToInt(max.x) - Mathf.FloorToInt(min.x);
            int height = Mathf.CeilToInt(max.y) - Mathf.FloorToInt(min.y);

            SetPixels(texture, pixelData,
                Mathf.FloorToInt(min.x),
                Mathf.FloorToInt(min.y),
                width,
                height
                );
        }

        public static void SetPixels(Texture2D texture, Color[] pixelData, Vector2Int min, Vector2Int max)
        {
            SetPixels(texture, pixelData, min.x, min.y, max.x - min.x, max.y - min.y);
        }

        public static void SetPixels(Texture2D texture, Color[] pixelData, int xMin, int yMin, int width, int height)
        {
            if (texture.isReadable)
            {
                texture.SetPixels(xMin, yMin, width, height, pixelData);
            }

            var info = MakeTextureAssetReadableAndUncompressed(texture);
            if (info.success)
            {
                texture.SetPixels(xMin, yMin, width, height, pixelData);
            }
        }

        public static bool SaveTextureAsPNG(Texture2D texture, string path = null)
        {
            if (path == null)
                path = AssetDatabase.GetAssetPath(texture);

            if (path != null)
            {
                // Make sure path ends with png
                path = System.IO.Path.ChangeExtension(path, ".png");

                // Write as png
                System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
                return true;
            }

            return false;
        }

        public static bool IsCompressed(Texture2D texture)
        {
            switch (texture.format)
            {
                case TextureFormat.Alpha8:
                case TextureFormat.ARGB4444:
                case TextureFormat.RGB24:
                case TextureFormat.RGBA32:
                case TextureFormat.ARGB32:
                case TextureFormat.RGB565:
                case TextureFormat.R16:
                    return false;

                case TextureFormat.DXT1:
                case TextureFormat.DXT5:
                    return true;

                case TextureFormat.RGBA4444:
                case TextureFormat.BGRA32:
                case TextureFormat.RHalf:
                case TextureFormat.RGHalf:
                case TextureFormat.RGBAHalf:
                case TextureFormat.RFloat:
                case TextureFormat.RGFloat:
                case TextureFormat.RGBAFloat:
                case TextureFormat.YUY2:
                case TextureFormat.RGB9e5Float:
                    return false;

                case TextureFormat.BC4:
                case TextureFormat.BC5:
                case TextureFormat.BC6H:
                case TextureFormat.BC7:
                case TextureFormat.DXT1Crunched:
                case TextureFormat.DXT5Crunched:
                case TextureFormat.PVRTC_RGB2:
                case TextureFormat.PVRTC_RGBA2:
                case TextureFormat.PVRTC_RGB4:
                case TextureFormat.PVRTC_RGBA4:
                case TextureFormat.ETC_RGB4:
                    return true;

                //case TextureFormat.ATC_RGB4:
                //    break;
                //case TextureFormat.ATC_RGBA8:
                //    break;
                case TextureFormat.EAC_R:
                case TextureFormat.EAC_R_SIGNED:
                case TextureFormat.EAC_RG:
                case TextureFormat.EAC_RG_SIGNED:
                case TextureFormat.ETC2_RGB:
                case TextureFormat.ETC2_RGBA1:
                case TextureFormat.ETC2_RGBA8:
                case TextureFormat.ASTC_4x4:
                case TextureFormat.ASTC_5x5:
                case TextureFormat.ASTC_6x6:
                case TextureFormat.ASTC_8x8:
                case TextureFormat.ASTC_10x10:
                case TextureFormat.ASTC_12x12:
#if !UNITY_2023_1_OR_NEWER
#pragma warning disable CS0618 // Type or member is obsolete
                case TextureFormat.ETC_RGB4_3DS:
                case TextureFormat.ETC_RGBA8_3DS:
                    return true;
#pragma warning restore CS0618 // Type or member is obsolete
#endif

                case TextureFormat.RG16:
                case TextureFormat.R8:
                    return false;

                case TextureFormat.ETC_RGB4Crunched:
                case TextureFormat.ETC2_RGBA8Crunched:
                case TextureFormat.ASTC_HDR_4x4:
                case TextureFormat.ASTC_HDR_5x5:
                case TextureFormat.ASTC_HDR_6x6:
                case TextureFormat.ASTC_HDR_8x8:
                case TextureFormat.ASTC_HDR_10x10:
                case TextureFormat.ASTC_HDR_12x12:
                    return true;

                case TextureFormat.RG32:
                case TextureFormat.RGB48:
                case TextureFormat.RGBA64:
                    return false;

                //case TextureFormat.ASTC_RGB_4x4:
                //    break;
                //case TextureFormat.ASTC_RGB_5x5:
                //    break;
                //case TextureFormat.ASTC_RGB_6x6:
                //    break;
                //case TextureFormat.ASTC_RGB_8x8:
                //    break;
                //case TextureFormat.ASTC_RGB_10x10:
                //    break;
                //case TextureFormat.ASTC_RGB_12x12:
                //    break;

#if !UNITY_2023_1_OR_NEWER
#pragma warning disable CS0618 // Type or member is obsolete
                case TextureFormat.ASTC_RGBA_4x4:
                case TextureFormat.ASTC_RGBA_5x5:
                case TextureFormat.ASTC_RGBA_6x6:
                case TextureFormat.ASTC_RGBA_8x8:
                case TextureFormat.ASTC_RGBA_10x10:
                case TextureFormat.ASTC_RGBA_12x12:
                    return true;
#pragma warning restore CS0618 // Type or member is obsolete
#endif

                //case TextureFormat.PVRTC_2BPP_RGB:
                //    break;
                //case TextureFormat.PVRTC_2BPP_RGBA:
                //    break;
                //case TextureFormat.PVRTC_4BPP_RGB:
                //    break;
                //case TextureFormat.PVRTC_4BPP_RGBA:
                //    break;

                default:
                    return false;
            }
        }
    }
}
