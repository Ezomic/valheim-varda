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
        internal static ConfigEntry<bool> RemoveDestroyedPortals;
        internal static ConfigEntry<bool> NameDungeons;
        internal static ConfigEntry<Minimap.PinType> DungeonPinType;
        internal static ConfigEntry<Minimap.PinType> PortalPinType;
        internal static ConfigEntry<string> DungeonIcon;
        internal static ConfigEntry<string> PortalIcon;
        internal static ConfigEntry<float> MergeRadius;
        internal static ConfigEntry<float> PortalMergeRadius;
        internal static ConfigEntry<bool> DumpIcons;
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

            // On, because a pin for a portal that is not there any more is the map lying about
            // your own network, and it is the one Varda pin that goes wrong by itself: a crypt
            // does not stop being a crypt. Off is for anyone who would rather keep the mark as a
            // record of where a portal used to stand.
            //
            // The second sentence of the description is the one that matters in play. A portal
            // that went while you were far away never told this machine anything, so its pin can
            // only come off when you are back, and "back" has to include the area having finished
            // loading, or a portal that simply has not arrived yet would lose its pin.
            //
            // Does nothing with Portals off. What puts back a pin that was taken off by mistake
            // is the portal pinning itself again, and that is the half Portals switches off.
            RemoveDestroyedPortals = cfg.Bind("Varda", "RemoveDestroyedPortals", true,
                "Take a portal's pin off your map when that portal is destroyed, by your hammer, "
                + "by damage or by anybody else. If it went while you were far away, the pin comes "
                + "off the next time you stand near where it was, once the area has finished "
                + "loading. Walking out of sight of a portal never counts. Only pins Varda put on "
                + "portals you built: a pin you placed by hand is never touched, and neither is a "
                + "dungeon pin. Does nothing while Portals is off.");

            // Off, and it was on until Robbin saw it in game. The icon already says what the
            // thing is, and a label under it repeats that in words while taking up room on a
            // map whose whole job is to be glanceable. Pin names draw on the large map only,
            // never on the minimap, so this is about the map you read when you are planning.
            NameDungeons = cfg.Bind("Varda", "NameDungeons", false,
                "Put the dungeon's own name under its pin - the same name the banner shows as "
                + "you go in. Off, because the icon already says what it is. On is worth it if "
                + "you want to tell two crypts apart without opening them.");

            // Which vanilla type a pin is saved as decides two things: which filter row hides
            // it, and what it degrades to if Varda is ever removed. It is NOT what the pin
            // looks like while the mod is running - the icon below wins.
            //
            // A custom PinType was the obvious alternative and it is a trap. Pins are written
            // to the map file as a bare int and read back through Minimap.AddPin, which in 1.0
            // takes nothing past the end of the enum: it logs "Trying to add invalid pin type"
            // and makes the pin Icon3 instead. So a custom type needs a patch on AddPin just to
            // exist, and without the mod every such pin loads as the plain marker, all of them
            // alike and filed under Icon3's row. Saving as a vanilla type means uninstalling
            // costs you a picture and nothing else.
            // Icon3 and Icon4 are not guesses any more. Read in game on 2026-09-21, the five
            // hand-placed slots are Icon0 fire, Icon1 house, Icon2 hammer, Icon3 plain marker,
            // Icon4 portal.
            //
            // Icon3 for dungeons because it is the one with no meaning of its own. Icon2 was
            // the first choice and is wrong: the hammer is what players already use to mark a
            // mine, so a dungeon pin would both look like somebody's own mining pin and hide
            // with it when that filter row is switched off.
            DungeonPinType = cfg.Bind("Varda", "DungeonPinType", Minimap.PinType.Icon3,
                "Which of the five hand-placed pin types a dungeon pin is saved as. This is "
                + "what it turns into if you uninstall Varda, and which filter row hides it. "
                + "The five are Icon0 fire, Icon1 house, Icon2 hammer, Icon3 plain marker, "
                + "Icon4 portal.");

            // Icon4 is the portal icon, so a portal pin is already drawn correctly with no
            // art of our own. That is why PortalIcon below is empty by default and
            // DungeonIcon is not.
            PortalPinType = cfg.Bind("Varda", "PortalPinType", Minimap.PinType.Icon4,
                "The same, for portal pins. Icon4 is the game's own portal icon, which is "
                + "already the right picture - there is no reason to change this one.");

            // A file name rather than a switch, so a rejected icon is swapped by dropping a
            // different PNG in and editing one line.
            DungeonIcon = cfg.Bind("Varda", "DungeonIcon", "dungeon.png",
                "PNG beside Varda.dll to draw dungeon pins with. Missing file, or empty, falls "
                + "back to the icon the game already uses for a crypt on the map, and failing "
                + "that to the plain pin above. Nothing here is ever fatal.");

            // Empty on purpose, unlike DungeonIcon. Icon4 already wears the game's own portal
            // icon, so drawing one would be replacing correct vanilla art with a copy of it.
            PortalIcon = cfg.Bind("Varda", "PortalIcon", "",
                "PNG beside Varda.dll to draw portal pins with. Empty by default because "
                + "PortalPinType is already the game's portal icon, which is the right "
                + "picture. Only worth setting if you want portals to stand out from the ones "
                + "you pin by hand.");

            // 8m because a dungeon entrance is a few metres across, and because re-entering the
            // same crypt must not stack a second pin on the first.
            //
            // Dungeons only. It was the portals' radius as well, and two of your portals inside
            // it shared one pin: with different tags each renamed that pin to its own twice a
            // second, so a row of tagged portals showed one pin flicking between their names.
            // Every portal has its own pin now, and PortalMergeRadius below is what is left of
            // this rule for them.
            MergeRadius = cfg.Bind("Varda", "MergeRadius", 8f,
                "Metres, for dungeons. A dungeon pin is not added when a pin already sits this "
                + "close to the door, whoever put it there. So re-entering a crypt does not stack "
                + "pins, and a pin you placed by hand on the door is left alone rather than "
                + "doubled. Portals have their own setting, PortalMergeRadius.");

            // A metre, and not the 8 it inherited, because all that is left for this to do is
            // keep a portal from doubling a pin somebody already put on it. Portals are built
            // side by side, a few metres apart, and at 8m one pin you placed in the middle of a
            // row would have left every portal round it without a pin of its own. A metre is
            // the distance the game itself treats as the same pin: Minimap.AddSharedMapData skips
            // a pin from a cartography table when a pin already on your map is within 1m of it,
            // which is the same "a pin already sits here" question as this one. In an ordinary
            // row it also keeps a pin standing in for the portal it is on and no other: to be
            // within a metre of two portals, a pin needs their centres under two metres apart,
            // and portals built side by side stand a few metres apart. Nothing has measured how
            // close two portals can be placed, so this is the ordinary row and not a guarantee.
            //
            // The cost is a pin placed by hand a couple of metres off the portal, which is now
            // doubled rather than respected. That is visible, and fixed by deleting the hand
            // pin, since Varda's follows the tag; a row of portals missing their pins for a
            // reason nobody can see is neither.
            PortalMergeRadius = cfg.Bind("Varda", "PortalMergeRadius", 1f,
                "Metres, for portals. A portal of yours gets no pin of its own while another pin "
                + "already sits this close to it, such as one you placed on it by hand, so the two "
                + "are not doubled. The pins Varda put on your other portals never count, so "
                + "portals built side by side each keep their own. One metre is what the game "
                + "itself treats as the same spot for a pin, and in an ordinary row, with portals "
                + "a few metres apart, a pin that close stands in only for the portal it is on. "
                + "Raising it respects hand pins placed less exactly, but keep it under half the "
                + "gap between your portals, or one pin will leave its neighbours without theirs.");

            // A tool for drawing art, not a feature, which is why it is off and why it writes
            // once and says so. The pin sprites are serialised on the Minimap prefab inside a
            // bundle, so the running game is the only place they can be read from at all.
            DumpIcons = cfg.Bind("Varda", "DumpIcons", false,
                "Write the game's own map pin art to BepInEx/config/Varda/icons/ once, and log "
                + "where each pin's picture sits on the sheet. For drawing an icon that "
                + "matches; turn it off again afterwards. It writes whole sheets, not cropped "
                + "pins, because cropping needs a vertical flip that is easy to get silently "
                + "wrong.");

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
