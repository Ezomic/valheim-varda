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
    /// icons be put back, and it is the same list the hand-placed icon set will need. It is also
    /// what tells a portal's pin from the one on the portal beside it, and what lets a destroyed
    /// portal take its own pin with it and nobody else's. And it is where a portal you hid from
    /// your map is written down, since that choice has no pin to carry it.
    ///
    /// <b>Why not a custom PinType instead, which would carry the icon by itself.</b> Because
    /// Minimap.AddPin will not keep a type past the end of the enum (it logs a warning and makes
    /// the pin Icon3), and the map file is read back through AddPin. So a custom type needs a
    /// patch on the pin system just to exist, and removing the mod would turn every one of
    /// those pins into the same plain marker on load. A file that goes stale is a much smaller
    /// problem than a map whose pins all stop saying what they were.
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

        /// <summary>The two the mod places itself. Anything else is an icon the player picked, or HiddenPortal.</summary>
        internal const string Dungeon = "dungeon";
        internal const string Portal = "portal";

        /// <summary>
        /// Not a pin at all: a portal of yours that you hid from your map with HidePortalKey, on
        /// the portal's own spot. See Hiding.
        ///
        /// A kind of its own rather than a mark on the portal's pin record, because the whole
        /// point of it is that there is no pin, and every question asked about Portal entries
        /// is a question about a pin: Has(pos, Portal) is how Tick tells Varda's pins from
        /// yours, Apply drops a Portal entry with no pin under it, and both moments of a
        /// destroyed portal take a pin off by it. Nothing that asks for Portal can see this, and
        /// Apply passes over it rather than looking for an icon file of that name.
        /// </summary>
        internal const string HiddenPortal = "hidden-portal";

        private static readonly List<Entry> Entries = new List<Entry>();
        private static string _path;

        /// <summary>
        /// Forgotten, and still written to the file, because the map saved on disk still has
        /// their pins. See Forget. Leaving holds what was forgotten since the map was last copied
        /// into the profile; Taken holds what that copy left out, waiting for the profile to reach
        /// disk.
        /// </summary>
        private static readonly List<Entry> Leaving = new List<Entry>();
        private static readonly List<Entry> Taken = new List<Entry>();

        /// <summary>
        /// The world session Leaving and Taken belong to. ZDOMan is built new for every world
        /// load and is a plain object, so it tells a respawn from a fresh login without Unity's
        /// null rules getting involved.
        /// </summary>
        private static ZDOMan _session;

        /// <summary>
        /// Reads the file for the world and character now in play. Called once the player is
        /// in the world, because the file name needs both names and neither exists before then.
        /// </summary>
        internal static void Load()
        {
            string path = PathFor();

            // A respawn after death reads the same file again in the same session, and that file
            // still names every spot forgotten since the map was last saved. Those stay forgotten
            // until the save. Anything else starts clean: a forget that never reached a saved
            // map never happened as far as the disk is concerned, the pin came back with the map,
            // and the file is right to name it.
            if (path == null || path != _path || ZDOMan.instance != _session)
            {
                Leaving.Clear();
                Taken.Clear();
            }

            _session = ZDOMan.instance;
            Entries.Clear();
            _path = path;

            if (path == null) return;
            if (!File.Exists(path)) return;

            var unclaimed = new List<Entry>(Leaving);
            unclaimed.AddRange(Taken);

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

                    var entry = new Entry { Icon = icon, X = x, Z = z };
                    if (Departing(entry, unclaimed)) continue;

                    Entries.Add(entry);
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
        /// that no longer has a pin on its spot, so deleting a pin by hand cleans the file up on
        /// the next load rather than leaving it to grow for the life of the character.
        ///
        /// The pin on the entry's very spot, never the nearest one. A nearest-within-a-radius
        /// search dressed whichever pin the map's list held first, which beside a portal could
        /// be one the player placed by hand, and it kept an entry alive on the strength of a pin
        /// that was not Varda's at all.
        ///
        /// A hidden portal is passed over. It has no pin by design, so the rule that cleans up
        /// after a pin deleted by hand would forget every portal you hid on the next load, and
        /// each would pin itself again. Its entry goes when the portal does, or when you show it.
        /// </summary>
        internal static void Apply()
        {
            if (Entries.Count == 0) return;

            int dressed = 0;

            for (int i = Entries.Count - 1; i >= 0; i--)
            {
                Entry entry = Entries[i];
                if (entry.Icon == HiddenPortal) continue;

                var pos = new Vector3(entry.X, 0f, entry.Z);

                Minimap.PinData pin = Pins.At(pos);
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

        /// <summary>
        /// Every remembered position wearing <paramref name="icon"/>, flat, as a copy, so a
        /// caller can Forget while it walks the list.
        /// </summary>
        internal static List<Vector3> Of(string icon)
        {
            var found = new List<Vector3>();

            foreach (Entry entry in Entries)
            {
                if (entry.Icon == icon) found.Add(new Vector3(entry.X, 0f, entry.Z));
            }

            return found;
        }

        /// <summary>
        /// Drops the entry for one spot from everything Varda asks this session. True when there
        /// was one to drop.
        ///
        /// Not from the file, yet. The pin's removal only reaches disk when the game next saves
        /// the profile (at logout, after sleeping, or every half hour), and a crash before then
        /// brings the pin back from the older map file. Had the file already let go of it, that
        /// pin would come back as nobody's: never taken off when its portal is found gone, never
        /// renamed with the tag. So the entry is written on until the map without its pin has
        /// been saved, which <see cref="MapTaken"/> and <see cref="MapWritten"/> follow, and after
        /// a crash it is still Varda's pin and is taken off again.
        ///
        /// Matched on the icon as well as the place, so forgetting a portal can never take a
        /// dungeon's entry with it. The spot is expected to have come out of Of, so the numbers
        /// are the same numbers and the tolerance is only there for float noise.
        /// </summary>
        internal static bool Forget(Vector3 pos, string icon)
        {
            var wanted = new Entry { Icon = icon, X = pos.x, Z = pos.z };
            bool dropped = false;

            for (int i = Entries.Count - 1; i >= 0; i--)
            {
                if (!Same(Entries[i], wanted)) continue;

                Leaving.Add(Entries[i]);
                Entries.RemoveAt(i);
                dropped = true;
            }

            // No Save. The file names the same spots as before, only on a different list.
            return dropped;
        }

        /// <summary>
        /// Drops a record with no pin behind it, file and all, at once. True when there was one.
        /// For HiddenPortal, and for nothing that stands for a pin: those go through Forget.
        ///
        /// Forget's wait for the map to be saved exists because the map file on disk can still
        /// hold the pin, and the entry is what says that pin is Varda's. A hidden portal's record
        /// says the opposite, that there is no pin, so there is nothing on disk for it to vouch
        /// for, and keeping it until the next save would only mean a crash could bring back a
        /// choice you had already undone.
        ///
        /// On the same spot as Has means it, to within Pins.SameSpot, so the two cannot disagree
        /// about which record is this portal's.
        /// </summary>
        internal static bool Drop(Vector3 pos, string icon)
        {
            const float near = Pins.SameSpot * Pins.SameSpot;
            bool dropped = false;

            for (int i = Entries.Count - 1; i >= 0; i--)
            {
                Entry entry = Entries[i];
                if (entry.Icon != icon) continue;

                float dx = entry.X - pos.x;
                float dz = entry.Z - pos.z;
                if (dx * dx + dz * dz >= near) continue;

                Entries.RemoveAt(i);
                dropped = true;
            }

            if (dropped) Save();
            return dropped;
        }

        /// <summary>
        /// The map has just been copied into the profile, from Minimap.SaveMapData, so every
        /// pin taken off before now is missing from that copy.
        /// </summary>
        internal static void MapTaken()
        {
            Taken.AddRange(Leaving);
            Leaving.Clear();
        }

        /// <summary>
        /// The profile holding that copy has been saved, from Game.SavePlayerProfile, so the file
        /// can let go of what the copy left out.
        ///
        /// SavePlayerProfile ignores whether PlayerProfile.Save managed to write, and so does
        /// this. The one case that costs is a failed disk write followed by a crash, which leaves
        /// a pin nobody claims, as before this existed.
        /// </summary>
        internal static void MapWritten()
        {
            if (Taken.Count == 0) return;

            Taken.Clear();
            Save();
        }

        /// <summary>
        /// Whether a line read back from the file is one this session has forgotten and is only
        /// still writing for the map's sake. Each forgotten entry answers for one line, so two
        /// entries on the same spot are not both swallowed by one forget.
        /// </summary>
        private static bool Departing(Entry entry, List<Entry> unclaimed)
        {
            for (int i = 0; i < unclaimed.Count; i++)
            {
                if (!Same(unclaimed[i], entry)) continue;

                unclaimed.RemoveAt(i);
                return true;
            }

            return false;
        }

        private static bool Same(Entry a, Entry b)
        {
            const float hair = 0.01f;

            return a.Icon == b.Icon
                && Mathf.Abs(a.X - b.X) <= hair
                && Mathf.Abs(a.Z - b.Z) <= hair;
        }

        /// <summary>
        /// Whether Varda put a pin wearing <paramref name="icon"/> on this very spot.
        ///
        /// The kind and the exact spot, never "anything of ours nearby". The nearby version let
        /// a portal claim whatever Varda pin sat within MergeRadius of it: the pin on the portal
        /// beside it, so that two portals with different tags renamed one pin back and forth
        /// twice a second, or a dungeon pin, which the portal then renamed to its own tag.
        /// </summary>
        internal static bool Has(Vector3 pos, string icon)
        {
            const float near = Pins.SameSpot * Pins.SameSpot;

            foreach (Entry entry in Entries)
            {
                if (entry.Icon != icon) continue;

                float dx = entry.X - pos.x;
                float dz = entry.Z - pos.z;
                if (dx * dx + dz * dz < near) return true;
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
                // Leaving and Taken as well, for the reason Forget gives.
                var text = new StringBuilder();
                foreach (List<Entry> list in new[] { Entries, Leaving, Taken })
                {
                    foreach (Entry entry in list)
                    {
                        text.Append(entry.Icon).Append(';')
                            .Append(entry.X.ToString("R", CultureInfo.InvariantCulture)).Append(';')
                            .Append(entry.Z.ToString("R", CultureInfo.InvariantCulture)).Append('\n');
                    }
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
