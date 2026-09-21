using BepInEx.Configuration;

namespace Varda
{
    /// <summary>
    /// Everything tunable, bound in one place so the .cfg reads as a document rather than as
    /// whatever order the code happened to need things in.
    ///
    /// The standing BepInEx trap applies here as everywhere: every entry is written to disk on
    /// first run and the saved value beats a new default in code. Changing a default does
    /// nothing on a machine that has already run the plugin - edit
    /// <c>&lt;profile&gt;\BepInEx\config\ezomic.valheim.varda.cfg</c> as part of the same
    /// change. When a config-driven change appears to do nothing in game, read the cfg before
    /// reading any code.
    /// </summary>
    internal static class VardaConfig
    {
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<bool> Dungeons;
        internal static ConfigEntry<bool> Portals;
        internal static ConfigEntry<bool> NameDungeons;
        internal static ConfigEntry<Minimap.PinType> DungeonPinType;
        internal static ConfigEntry<Minimap.PinType> PortalPinType;
        internal static ConfigEntry<string> DungeonIcon;
        internal static ConfigEntry<string> PortalIcon;
        internal static ConfigEntry<float> MergeRadius;
        internal static ConfigEntry<bool> Verbose;

        internal static void Bind(ConfigFile cfg)
        {
            // Loaded, bound, patched, and deciding nothing. Not "unloaded" - a plugin cannot
            // unload itself, and a switch that pretends otherwise is a lie somebody will debug.
            Enabled = cfg.Bind("Varda", "Enabled", true,
                "Off leaves the plugin loaded and stops it adding anything to the map. Pins "
                + "already on the map stay - they are ordinary map pins and nothing removes "
                + "them but you.");

            // The rule the whole mod is built on, and it is worth stating in the file a player
            // reads rather than only in the README: nothing is pinned that you have not
            // physically been through. An ore detector is a different mod and a worse one.
            Dungeons = cfg.Bind("Varda", "Dungeons", true,
                "Pin a burial chamber, cave, crypt or mine when you GO IN. Walking past the "
                + "door does nothing - the pin is a record of where you have been, never a "
                + "hint about where something is.");

            Portals = cfg.Bind("Varda", "Portals", true,
                "Pin portals you built, labelled with whatever you tagged them. Portals built "
                + "by other players are never pinned; you would be reading their map.");

            NameDungeons = cfg.Bind("Varda", "NameDungeons", true,
                "Put the dungeon's own name on its pin - the same name the banner shows as you "
                + "go in. Pin names only draw on the large map zoomed in, never on the minimap, "
                + "so this costs nothing at a glance.");

            // Which vanilla type a pin is saved as decides two things: which filter row hides
            // it, and what it degrades to if Varda is ever removed. It is NOT what the pin
            // looks like while the mod is running - the icon below wins.
            //
            // A custom PinType was the obvious alternative and it is a trap. Pins are written
            // to the map file as a bare int and read back through Minimap.AddPin, which
            // rejects anything past the end of the enum: remove the mod and every pin of that
            // type is dropped on load, silently, and the next save writes the map back without
            // them. Saving as a vanilla type means uninstalling costs you an icon, not a map.
            DungeonPinType = cfg.Bind("Varda", "DungeonPinType", Minimap.PinType.Icon2,
                "Which of the five hand-placed pin types a dungeon pin is saved as. This is "
                + "what it turns into if you uninstall Varda, and which filter row hides it. "
                + "Which sprite each number wears is asset data - turn on Verbose and the log "
                + "names them all at the first map load.");

            PortalPinType = cfg.Bind("Varda", "PortalPinType", Minimap.PinType.Icon4,
                "The same, for portal pins. Worth keeping different from the dungeon type so "
                + "the two can be filtered apart.");

            // A file name rather than a switch, so a rejected icon is swapped by dropping a
            // different PNG in and editing one line.
            DungeonIcon = cfg.Bind("Varda", "DungeonIcon", "dungeon.png",
                "PNG beside Varda.dll to draw dungeon pins with. Missing file, or empty, falls "
                + "back to the icon the game already uses for a crypt on the map, and failing "
                + "that to the plain pin above. Nothing here is ever fatal.");

            PortalIcon = cfg.Bind("Varda", "PortalIcon", "portal.png",
                "PNG beside Varda.dll to draw portal pins with. Missing or empty leaves the "
                + "plain pin above.");

            // 8m because a dungeon entrance is a few metres across and a portal is two, and
            // because re-entering the same crypt must not stack a second pin on the first.
            MergeRadius = cfg.Bind("Varda", "MergeRadius", 8f,
                "Metres. A pin is not added when one already sits this close, whoever put it "
                + "there - so re-entering a crypt does not stack pins, and a pin you placed by "
                + "hand on the door is left alone rather than doubled.");

            // Not synced by intent - see the plugin. A diagnostic flag is personal, and a host
            // turning on someone else's logging is not a thing anybody asked for.
            Verbose = cfg.Bind("Varda", "Verbose", false,
                "Write every pin added, every icon resolved and the name of every sprite the "
                + "map carries to BepInEx/LogOutput.log. The sprite list is how you find out "
                + "which PinType wears which icon, which is asset data and not readable "
                + "anywhere else.");
        }
    }
}
