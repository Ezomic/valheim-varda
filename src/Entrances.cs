using UnityEngine;

namespace Varda
{
    /// <summary>
    /// Pins a dungeon when you go into it.
    ///
    /// <b>Entering is one method.</b> <c>Teleport.Interact</c> is what runs whether you press
    /// Use on the door or simply walk into it - the component's own OnTriggerEnter calls
    /// Interact - and it returns true only when the move actually happened, so a boss-blocked
    /// portal or a missing target never leaves a pin behind. There is nothing else to hook.
    ///
    /// <b>Two things have to be read before the teleport and one after.</b> By the time it
    /// returns true the player is inside, and a dungeon interior sits above y 3000 directly
    /// over its entrance - so reading the position afterwards would pin a point three
    /// kilometres in the air, and reading InInterior afterwards would say you were inside
    /// whichever direction you went. Hence a prefix that captures, and a postfix that acts.
    ///
    /// <b>And only inbound.</b> The same component pairs an entrance with an exit: going out
    /// is the same call on the far end. <c>character.InInterior()</c> before the move is what
    /// separates them, and it is why a crypt leaves exactly one pin however many times you go
    /// in and out.
    /// </summary>
    internal static class Entrances
    {
        /// <summary>What the prefix hands the postfix. A struct, so there is nothing to null.</summary>
        internal struct Entry
        {
            internal bool Eligible;
            internal Vector3 Pos;
            internal string Name;
        }

        internal static Entry Before(Teleport teleport, Humanoid character)
        {
            var entry = default(Entry);

            if (!VardaConfig.Enabled.Value || !VardaConfig.Dungeons.Value) return entry;

            // Every Teleport in the loaded scene runs this, not only the one in front of you,
            // and creatures do not use them - but the check is one comparison and its absence
            // is the classic way a patch acts for somebody else's character.
            if (character == null || Player.m_localPlayer == null) return entry;
            if ((Object)character != (Object)Player.m_localPlayer) return entry;

            // Outbound only. This is the whole of the entrance/exit distinction.
            if (character.InInterior()) return entry;

            if (teleport == null) return entry;

            entry.Eligible = true;
            entry.Pos = teleport.transform.position;
            entry.Name = NameOf(teleport);
            return entry;
        }

        internal static void After(Entry entry, bool moved)
        {
            if (!entry.Eligible || !moved) return;

            float radius = VardaConfig.MergeRadius.Value;

            // A pin already here is left exactly as it is, whoever put it there. Re-entering a
            // crypt must not stack pins, and a pin the player put on the door by hand is
            // theirs - overwriting its name or its icon would be this mod editing the player's
            // map rather than adding to it.
            if (Pins.Near(entry.Pos, radius) != null)
            {
                if (VardaConfig.Verbose.Value)
                {
                    VardaPlugin.Log.LogInfo(
                        "A pin is already within " + radius + "m of " + entry.Name + " - left alone.");
                }

                return;
            }

            string label = VardaConfig.NameDungeons.Value ? entry.Name : "";

            Minimap.PinData pin = Pins.Add(
                entry.Pos, VardaConfig.DungeonPinType.Value, label, Icons.Dungeon());

            if (pin == null) return;

            Remembered.Note(entry.Pos, Remembered.Dungeon);

            if (VardaConfig.Verbose.Value)
            {
                VardaPlugin.Log.LogInfo("Pinned " + entry.Name + " at " + entry.Pos);
            }
        }

        /// <summary>
        /// The dungeon's own name, which the game already has: <c>m_enterText</c> is the
        /// localisation key it shows as a banner on the way in. Taking it from there means a
        /// modded dungeon is named correctly for free and nothing here needs a lookup table.
        /// </summary>
        private static string NameOf(Teleport teleport)
        {
            string key = teleport.m_enterText;

            if (!string.IsNullOrEmpty(key) && Localization.instance != null)
            {
                string text = Localization.instance.Localize(key);
                if (!string.IsNullOrEmpty(text)) return text;
            }

            // Falls back to the piece of the world this door belongs to rather than to a
            // constant, because a dungeon with no banner text is still a named location.
            Location location = teleport.GetComponentInParent<Location>();
            if (location != null && !string.IsNullOrEmpty(location.name))
            {
                return location.name.Replace("(Clone)", "");
            }

            return "Dungeon";
        }
    }
}
