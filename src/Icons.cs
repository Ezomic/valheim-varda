using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using UnityEngine;

namespace Varda
{
    /// <summary>
    /// Where a pin's picture comes from, in order of preference: a PNG dropped beside the DLL,
    /// then a sprite the game already carries, then nothing - which leaves the pin wearing the
    /// vanilla icon for its type and is a perfectly usable mod.
    ///
    /// Nothing here is ever fatal, and that is the point. A missing file, an unreadable file
    /// and a game update that renames a location all end the same way: one line in the log and
    /// a map that still works.
    /// </summary>
    internal static class Icons
    {
        /// <summary>
        /// Entries in the map's own location-icon table to lift a dungeon sprite from, best
        /// first. All of them are dungeon-ish, so a crypt is never marked with a merchant or a
        /// boss stone, and borrowing rather than drawing means this follows an art update.
        /// </summary>
        private static readonly string[] DungeonDonors =
        {
            "Crypt2", "Crypt3", "Crypt4", "SunkenCrypt4", "MountainCave02",
        };

        private static Sprite _dungeon;
        private static Sprite _portal;
        private static bool _resolved;

        /// <summary>
        /// Our own sprites, made from our own textures, kept across worlds. The borrowed one is
        /// not: in 1.0 the soft-ref bundles unload at logout and destroy the assets in them, so
        /// a game sprite cached across a trip to the main menu is a destroyed object that
        /// compares equal to null in some places and throws in others.
        /// </summary>
        private static readonly Dictionary<string, Sprite> Loaded = new Dictionary<string, Sprite>();

        /// <summary>Called when a new map comes up, because the borrowed half does not survive.</summary>
        internal static void Reset()
        {
            _dungeon = null;
            _portal = null;
            _resolved = false;
        }

        /// <summary>
        /// Resolves both sprites now rather than at the first pin. Under Verbose that is also
        /// what writes the map's two sprite tables to the log, and there is nowhere else to
        /// read them - which pin type wears which picture is serialised on the Minimap prefab
        /// inside a bundle, so it is in neither the assemblies nor the asset manifest.
        /// </summary>
        internal static void Warm()
        {
            Resolve();
        }

        internal static Sprite Dungeon()
        {
            Resolve();
            return _dungeon;
        }

        internal static Sprite Portal()
        {
            Resolve();
            return _portal;
        }

        /// <summary>
        /// Any icon by name, loaded from &lt;name&gt;.png beside the DLL and cached.
        ///
        /// The two above are the mod's own and have a fallback each. This one has none on
        /// purpose: it serves icons the PLAYER chose from the picker, and there is no sensible
        /// substitute for the one they asked for. A null here means the pin keeps whatever
        /// picture its vanilla type wears, which is exactly what it would look like with the
        /// mod uninstalled - so the failure mode is the uninstalled one rather than a new one.
        /// </summary>
        internal static Sprite Named(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;

            return FromFile(name.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                ? name
                : name + ".png");
        }

        private static void Resolve()
        {
            if (_resolved) return;

            Minimap map = Minimap.instance;
            if (map == null) return;

            _resolved = true;

            // An explicit null check, not ??. Sprite derives from UnityEngine.Object, whose
            // == is overloaded so that a destroyed object compares equal to null - and the
            // null-propagating operators bypass that overload entirely, so ?? would keep a
            // destroyed sprite and throw somewhere unrelated later.
            _dungeon = FromFile(VardaConfig.DungeonIcon.Value);
            if (_dungeon == null) _dungeon = Borrow(map);

            _portal = FromFile(VardaConfig.PortalIcon.Value);

            if (!VardaConfig.Verbose.Value) return;

            // The map's two sprite tables, written out in full. Which PinType wears which
            // picture is asset data - it is not in the assemblies and not in the manifest, so
            // making the mod say what it is looking at is the only way to find out, and it is
            // what the DungeonPinType and PortalPinType settings need to be chosen sensibly.
            foreach (Minimap.SpriteData entry in map.m_icons)
            {
                VardaPlugin.Log.LogInfo(
                    "  pin type " + entry.m_name + " wears "
                    + (entry.m_icon == null ? "nothing" : entry.m_icon.name));
            }

            VardaPlugin.Log.LogInfo(
                "  " + map.m_locationIcons.Count + " location icons, dungeon sprite is "
                + (_dungeon == null ? "the plain pin" : _dungeon.name)
                + ", portal sprite is " + (_portal == null ? "the plain pin" : _portal.name));
        }

        /// <summary>The game's own crypt icon, if it still has one under a name we know.</summary>
        private static Sprite Borrow(Minimap map)
        {
            foreach (string donor in DungeonDonors)
            {
                foreach (Minimap.LocationSpriteData entry in map.m_locationIcons)
                {
                    if (entry.m_name == donor && entry.m_icon != null) return entry.m_icon;
                }
            }

            VardaPlugin.Log.LogInfo(
                "No dungeon icon to borrow - dungeon pins will wear the plain "
                + VardaConfig.DungeonPinType.Value + " icon. Drop a dungeon.png beside "
                + "Varda.dll to give them their own.");

            return null;
        }

        /// <summary>
        /// A PNG from the mod's own folder, cached by name. Null for an empty setting, a
        /// missing file or an unreadable one, each logged once by virtue of the cache.
        /// </summary>
        private static Sprite FromFile(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return null;

            Sprite cached;
            if (Loaded.TryGetValue(fileName, out cached)) return cached;

            Loaded[fileName] = null;

            string folder = Path.GetDirectoryName(typeof(Icons).Assembly.Location);
            if (string.IsNullOrEmpty(folder)) return null;

            string path = Path.Combine(folder, fileName);
            if (!File.Exists(path))
            {
                VardaPlugin.Log.LogInfo(
                    "No " + fileName + " beside the DLL - falling back for that pin. This is "
                    + "the normal state until an icon is drawn; it is not an error.");
                return null;
            }

            try
            {
                byte[] data = File.ReadAllBytes(path);

                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                {
                    // Point filtering and clamped, to match the game's art: Valheim sits near
                    // 128 texels a metre with flat colour bands, and a bilinear-filtered icon
                    // reads as blurry beside vanilla's.
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                };

                if (!LoadPng(texture, data))
                {
                    UnityEngine.Object.Destroy(texture);
                    VardaPlugin.Log.LogWarning(fileName + " is not a PNG this can read.");
                    return null;
                }

                Sprite sprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f));

                sprite.name = "Varda_" + Path.GetFileNameWithoutExtension(fileName);

                Loaded[fileName] = sprite;
                return sprite;
            }
            catch (Exception e)
            {
                VardaPlugin.Log.LogWarning("Could not read " + fileName + ": " + e.Message);
                return null;
            }
        }

        /// <summary>
        /// Texture2D.LoadImage, by reflection.
        ///
        /// It lives in UnityEngine.ImageConversionModule, which targets netstandard 2.1 while
        /// this builds against net462 - referencing it outright fails the build with CS1705.
        /// The method is present at runtime regardless, so reaching it this way costs one
        /// lookup and removes the whole problem.
        /// </summary>
        private static bool LoadPng(Texture2D texture, byte[] data)
        {
            Type type = AccessTools.TypeByName("UnityEngine.ImageConversion");
            if (type == null) return false;

            var method = AccessTools.Method(type, "LoadImage",
                             new[] { typeof(Texture2D), typeof(byte[]) })
                         ?? AccessTools.Method(type, "LoadImage",
                             new[] { typeof(Texture2D), typeof(byte[]), typeof(bool) });

            if (method == null) return false;

            object[] args = method.GetParameters().Length == 3
                ? new object[] { texture, data, false }
                : new object[] { texture, data };

            return (bool)method.Invoke(null, args);
        }
    }
}
