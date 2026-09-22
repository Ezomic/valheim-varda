using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using BepInEx;
using UnityEngine;

namespace Varda
{
    /// <summary>
    /// The list of pins Varda put there, kept in a small text file beside the config.
    ///
    /// <b>Why this file has to exist.</b> A pin is written into the map save as a name, a
    /// position and its type as a bare int - and nothing else. The picture is not saved,
    /// because vanilla derives it from the type. So on the next load our dungeon pins come
    /// back wearing whatever the plain icon for that type is, and the map quietly looks
    /// different from the map you left. Remembering which positions are ours is what lets the
    /// icons be put back, and it is the same list the hand-placed icon set will need.
    ///
    /// <b>Why not a custom PinType instead, which would carry the icon by itself.</b> Because
    /// Minimap.AddPin refuses any type past the end of the enum, and the map file is read back
    /// through AddPin - so removing the mod would drop every one of those pins on load,
    /// silently, and the next save would write the map back without them. A file that goes
    /// stale is a much smaller problem than a map that loses pins.
    ///
    /// One file per character per world, because that is the granularity the map itself has.
    /// </summary>
    internal static class Remembered
    {
        private struct Entry
        {
            /// <summary>Which picture this pin wears, as an icon name rather than a category.</summary>
            internal string Icon;
            internal float X;
            internal float Z;
        }

        /// <summary>The two the mod places itself. Anything else is an icon the player picked.</summary>
        internal const string Dungeon = "dungeon";
        internal const string Portal = "portal";

        private static readonly List<Entry> Entries = new List<Entry>();
        private static string _path;

        /// <summary>
        /// Reads the file for the world and character now in play. Called once the player is
        /// in the world, because the file name needs both names and neither exists before then.
        /// </summary>
        internal static void Load()
        {
            Entries.Clear();
            _path = null;

            string path = PathFor();
            if (path == null) return;

            _path = path;

            if (!File.Exists(path)) return;

            try
            {
                foreach (string line in File.ReadAllLines(path))
                {
                    string[] parts = line.Split(';');
                    if (parts.Length != 3) continue;

                    // Lowercased on the way in, which also reads the files written before the
                    // kind was an open set: those hold Dungeon and Portal with a capital, from
                    // an enum's ToString, and there is no reason to make anybody's map forget
                    // its icons over a letter.
                    string icon = parts[0].Trim().ToLowerInvariant();
                    if (icon.Length == 0) continue;

                    float x, z;

                    // InvariantCulture throughout. This machine is on a Dutch locale, where a
                    // comma is the decimal separator, and a file written on one locale and read
                    // on another would parse every coordinate wrong rather than failing.
                    if (!float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out x)) continue;
                    if (!float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out z)) continue;

                    Entries.Add(new Entry { Icon = icon, X = x, Z = z });
                }
            }
            catch (Exception e)
            {
                VardaPlugin.Log.LogWarning(
                    "Could not read " + path + " (" + e.Message + "). Pins already on the map "
                    + "keep their plain icons this session; nothing is lost from the map.");
            }

            if (VardaConfig.Verbose.Value)
            {
                VardaPlugin.Log.LogInfo("Remembering " + Entries.Count + " pins from " + path);
            }
        }

        /// <summary>Adds a position to the list and writes the file.</summary>
        internal static void Note(Vector3 pos, string icon)
        {
            if (string.IsNullOrEmpty(icon)) return;

            Entries.Add(new Entry { Icon = icon, X = pos.x, Z = pos.z });
            Save();
        }

        /// <summary>
        /// Puts our icons back on the pins that came out of the map file, and drops any entry
        /// that no longer has a pin - so deleting a pin by hand cleans the file up on the next
        /// load rather than leaving it to grow for the life of the character.
        /// </summary>
        internal static void Apply()
        {
            if (Entries.Count == 0) return;

            float radius = VardaConfig.MergeRadius.Value;
            int dressed = 0;

            for (int i = Entries.Count - 1; i >= 0; i--)
            {
                Entry entry = Entries[i];
                var pos = new Vector3(entry.X, 0f, entry.Z);

                Minimap.PinData pin = Pins.Near(pos, radius);
                if (pin == null)
                {
                    Entries.RemoveAt(i);
                    continue;
                }

                Sprite icon = SpriteFor(entry.Icon);
                if (icon == null) continue;

                Pins.Dress(pin, icon);
                dressed++;
            }

            Save();

            if (VardaConfig.Verbose.Value)
            {
                VardaPlugin.Log.LogInfo("Put Varda's icons back on " + dressed + " pins.");
            }
        }

        /// <summary>True when a position is already one of ours, whatever it looks like.</summary>
        internal static bool Holds(Vector3 pos, float radius)
        {
            foreach (Entry entry in Entries)
            {
                float dx = entry.X - pos.x;
                float dz = entry.Z - pos.z;
                if (dx * dx + dz * dz < radius * radius) return true;
            }

            return false;
        }

        /// <summary>
        /// The picture for a remembered icon name.
        ///
        /// The mod's own two go through Icons so they keep their fallbacks - a dungeon with no
        /// PNG drawn yet still borrows, and a portal with none keeps the game's portal icon.
        /// Everything else is a file the player chose, and has no substitute worth inventing.
        /// </summary>
        private static Sprite SpriteFor(string icon)
        {
            if (icon == Dungeon) return Icons.Dungeon();
            if (icon == Portal) return Icons.Portal();

            return Icons.Named(icon);
        }

        private static void Save()
        {
            if (_path == null) return;

            try
            {
                var text = new StringBuilder();
                foreach (Entry entry in Entries)
                {
                    text.Append(entry.Icon).Append(';')
                        .Append(entry.X.ToString("R", CultureInfo.InvariantCulture)).Append(';')
                        .Append(entry.Z.ToString("R", CultureInfo.InvariantCulture)).Append('\n');
                }

                string folder = Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                }

                // UTF8Encoding(false), explicitly. A BOM here would be read back as part of the
                // first line's kind and that entry would be dropped every time.
                File.WriteAllText(_path, text.ToString(), new UTF8Encoding(false));
            }
            catch (Exception e)
            {
                VardaPlugin.Log.LogWarning(
                    "Could not write " + _path + " (" + e.Message + "). The pins are on the "
                    + "map either way; what is at risk is their icons after the next load.");
            }
        }

        /// <summary>
        /// One file per character per world. Null while either is unknown, which is every frame
        /// before the player is actually in the world.
        /// </summary>
        private static string PathFor()
        {
            if (ZNet.instance == null || Player.m_localPlayer == null) return null;

            string world = Safe(ZNet.instance.GetWorldName());
            string character = Safe(Player.m_localPlayer.GetPlayerName());

            if (string.IsNullOrEmpty(world) || string.IsNullOrEmpty(character)) return null;

            return Path.Combine(Path.Combine(Paths.ConfigPath, "Varda"), world + "." + character + ".txt");
        }

        private static string Safe(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;

            foreach (char bad in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(bad, '_');
            }

            return name.Replace('.', '_');
        }
    }
}
