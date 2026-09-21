using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace Varda
{
    /// <summary>
    /// Writes the map's own pin art to disk, once, so a new icon can be drawn to match it
    /// rather than guessed at.
    ///
    /// <b>Why this has to exist at all.</b> The pin sprites are serialised on the Minimap
    /// prefab inside a hashed UnityFS bundle. They are in neither the assemblies nor the asset
    /// manifest, and Devkit's rip cannot reach them either, because that resolves names through
    /// ZNetScene and ObjectDB and the Minimap is in neither. The running game is the only
    /// place the art exists in a form anything can read, so the mod has to hand it over.
    ///
    /// <b>It writes whole textures, not cropped sprites, and logs each sprite's rect.</b>
    /// Cropping here would mean getting the vertical flip right against a RenderTexture
    /// readback, which is platform-dependent and silently produces a picture of the wrong part
    /// of the atlas. Writing the sheet and the numbers puts that step somewhere it can be
    /// looked at.
    ///
    /// Behind a config flag and off by default. This is a tool for drawing art, not a feature,
    /// and a player has no reason to have their disk written to.
    /// </summary>
    internal static class IconDump
    {
        private static bool _done;

        internal static void Reset()
        {
            _done = false;
        }

        internal static void Run()
        {
            if (_done || !VardaConfig.DumpIcons.Value) return;

            Minimap map = Minimap.instance;
            if (map == null) return;

            _done = true;

            string folder = Path.Combine(Path.Combine(Paths.ConfigPath, "Varda"), "icons");

            try
            {
                Directory.CreateDirectory(folder);
            }
            catch (Exception e)
            {
                VardaPlugin.Log.LogWarning("Cannot write " + folder + ": " + e.Message);
                return;
            }

            // Sheets already written, by texture name. Every pin icon is very likely one atlas,
            // so without this the same megabyte is written eighteen times.
            var written = new HashSet<string>();

            foreach (Minimap.SpriteData entry in map.m_icons)
            {
                Record(folder, written, entry.m_name.ToString(), entry.m_icon);
            }

            foreach (Minimap.LocationSpriteData entry in map.m_locationIcons)
            {
                Record(folder, written, "location_" + entry.m_name, entry.m_icon);
            }

            VardaPlugin.Log.LogInfo(
                "Icon dump finished: " + written.Count + " sheet(s) in " + folder
                + ". Turn DumpIcons off again.");
        }

        private static void Record(string folder, HashSet<string> written, string slot, Sprite sprite)
        {
            if (sprite == null)
            {
                VardaPlugin.Log.LogInfo("  " + slot + " has no sprite.");
                return;
            }

            Texture2D texture = sprite.texture;
            Rect rect = sprite.textureRect;

            VardaPlugin.Log.LogInfo(
                "  " + slot + " sprite=" + sprite.name
                + " sheet=" + (texture == null ? "none" : texture.name)
                + " rect=" + (int)rect.x + "," + (int)rect.y + "," + (int)rect.width + "," + (int)rect.height
                + " pixelsPerUnit=" + sprite.pixelsPerUnit);

            if (texture == null) return;

            string name = string.IsNullOrEmpty(texture.name) ? "sheet" : texture.name;
            if (!written.Add(name)) return;

            try
            {
                byte[] png = Encode(texture);
                if (png == null) return;

                File.WriteAllBytes(Path.Combine(folder, name + ".png"), png);
            }
            catch (Exception e)
            {
                VardaPlugin.Log.LogWarning("Could not write " + name + ".png: " + e.Message);
            }
        }

        /// <summary>
        /// Reads a texture the CPU is not allowed to touch and encodes it.
        ///
        /// Most textures ship with Read/Write disabled, so the pixels live on the GPU and
        /// <c>GetPixels</c> throws. Blitting into a RenderTexture and reading that back is the
        /// route around it, and it is why a texture is always recoverable where a mesh marked
        /// unreadable is not.
        /// </summary>
        private static byte[] Encode(Texture2D texture)
        {
            RenderTexture rt = RenderTexture.GetTemporary(
                texture.width, texture.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);

            RenderTexture previous = RenderTexture.active;
            Texture2D copy = null;

            try
            {
                Graphics.Blit(texture, rt);
                RenderTexture.active = rt;

                copy = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
                copy.ReadPixels(new Rect(0f, 0f, texture.width, texture.height), 0, 0);
                copy.Apply();

                return EncodePng(copy);
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);

                if (copy != null) UnityEngine.Object.Destroy(copy);
            }
        }

        /// <summary>
        /// ImageConversion.EncodeToPNG, by reflection, for the same reason Icons reaches
        /// LoadImage that way: the module targets netstandard 2.1 and this builds against
        /// net462, so referencing it fails the build with CS1705 while the method is present
        /// at runtime regardless.
        /// </summary>
        private static byte[] EncodePng(Texture2D texture)
        {
            Type type = AccessTools.TypeByName("UnityEngine.ImageConversion");
            if (type == null) return null;

            var method = AccessTools.Method(type, "EncodeToPNG", new[] { typeof(Texture2D) });
            if (method == null) return null;

            return (byte[])method.Invoke(null, new object[] { texture });
        }
    }
}
