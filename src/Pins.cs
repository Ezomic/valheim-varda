using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Varda
{
    /// <summary>
    /// The map, reduced to the few things this mod does to it: look for a pin near a point or
    /// exactly on one, add one, take one away, and put a borrowed sprite on one.
    ///
    /// Everything here goes through <c>Minimap.AddPin</c> and <c>Minimap.RemovePin</c>, which
    /// are public and are what the game itself uses - so a Varda pin is an ordinary pin in
    /// every respect a player can see. It can be renamed, ticked off, filtered and deleted by
    /// hand, and it is written into the map file with all the others.
    ///
    /// The one thing that needs reflection is reading the list. <c>m_pins</c> is private and
    /// so is <c>HavePinInRange</c>, and without them there is no way to ask "is there already
    /// a pin here" - which is the difference between entering a crypt twice and having two
    /// pins on the door.
    /// </summary>
    internal static class Pins
    {
        /// <summary>
        /// Bound on first use inside a try/catch rather than in a static initialiser.
        ///
        /// A FieldRefAccess with a wrong name or owner throws at type-init, and from then on
        /// every member of the declaring class throws TypeInitializationException - which
        /// surfaces as unrelated things breaking rather than as this line being wrong. Binding
        /// lazily means a rename in a game update costs the dedupe check and nothing else.
        /// </summary>
        private static AccessTools.FieldRef<Minimap, List<Minimap.PinData>> _pinList;
        private static bool _tried;

        private static List<Minimap.PinData> All()
        {
            Minimap map = Minimap.instance;
            if (map == null) return null;

            if (!_tried)
            {
                _tried = true;
                try
                {
                    _pinList = AccessTools.FieldRefAccess<Minimap, List<Minimap.PinData>>("m_pins");
                }
                catch (Exception e)
                {
                    VardaPlugin.Log.LogWarning(
                        "Cannot read the map's pin list (" + e.GetType().Name + "). Pins will "
                        + "still be added; what is lost is the check that stops a second pin "
                        + "landing on one that is already there.");
                }
            }

            if (_pinList == null) return null;

            try
            {
                return _pinList(map);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// The first saved pin within <paramref name="radius"/> metres, measured flat - height
        /// is ignored because a dungeon entrance and its pin can be several metres apart
        /// vertically on a hillside and are the same place for this purpose.
        ///
        /// Unsaved pins are skipped, which is the same rule vanilla's own HavePinInRange uses:
        /// the player marker, a shout, an event ring and the death marker are all pins, and
        /// none of them should stop a crypt being marked.
        /// </summary>
        internal static Minimap.PinData Near(Vector3 pos, float radius)
        {
            List<Minimap.PinData> pins = All();
            if (pins == null) return null;

            foreach (Minimap.PinData pin in pins)
            {
                if (!pin.m_save) continue;
                if (Utils.DistanceXZ(pos, pin.m_pos) < radius) return pin;
            }

            return null;
        }

        /// <summary>
        /// How far a pin may sit from a remembered spot and still be the pin Varda wrote there.
        /// Only float noise needs absorbing: AddPin keeps the position it is given, the map file
        /// writes it back as three floats, and the sidecar keeps the same numbers with "R".
        /// </summary>
        private const float SameSpot = 0.1f;

        /// <summary>
        /// The saved pin sitting on exactly this spot, or null.
        ///
        /// Not Near, and the difference is what keeps a hand-placed pin safe when Varda takes
        /// one away. Near answers with the first pin inside MergeRadius, and beside a portal that
        /// can be one the player put there; this answers only with a pin on the very point the
        /// sidecar remembers. A pin carrying an owner is skipped as well. Varda always writes 0,
        /// and a pin with somebody's id in it came off a cartography table.
        /// </summary>
        internal static Minimap.PinData At(Vector3 pos)
        {
            List<Minimap.PinData> pins = All();
            if (pins == null) return null;

            Minimap.PinData best = null;
            float closest = SameSpot;

            foreach (Minimap.PinData pin in pins)
            {
                if (!pin.m_save || pin.m_ownerID != 0L) continue;

                float distance = Utils.DistanceXZ(pos, pin.m_pos);
                if (distance >= closest) continue;

                closest = distance;
                best = pin;
            }

            return best;
        }

        /// <summary>
        /// Adds a pin and dresses it. Null if the map is not up yet, which is normal during a
        /// load and is not worth a warning.
        ///
        /// The sprite is written straight onto the PinData after AddPin rather than being
        /// registered against a pin type, because AddPin has already resolved the type's own
        /// sprite by the time it returns and the marker that draws it is not built until the
        /// map next updates. This is exactly what vanilla does for the boss altars and the
        /// trader - <c>UpdateLocationPins</c> adds a PinType.None pin and then overwrites
        /// <c>m_icon</c> - so it is a supported shape rather than a trick.
        /// </summary>
        internal static Minimap.PinData Add(Vector3 pos, Minimap.PinType type, string name, Sprite icon)
        {
            Minimap map = Minimap.instance;
            if (map == null) return null;

            // No null check on the result. In 1.0 AddPin never refuses: a type it does not know
            // is logged as "Trying to add invalid pin type" and saved as Icon3, so a bad
            // DungeonPinType or PortalPinType shows up as that warning and a plain marker.
            Minimap.PinData pin = map.AddPin(pos, type, name ?? "", true, false, 0L);

            Dress(pin, icon);
            return pin;
        }

        /// <summary>
        /// Puts a sprite on a pin that already exists, and refreshes the marker if the map is
        /// open and has already drawn it.
        ///
        /// The second half matters on a world load: the pins come back from the map file
        /// wearing their saved type's own sprite, and if the map has been opened before the
        /// icons are re-applied then the markers are already built and would keep it.
        /// </summary>
        internal static void Dress(Minimap.PinData pin, Sprite icon)
        {
            if (pin == null || icon == null) return;

            pin.m_icon = icon;

            if (pin.m_iconElement != null) pin.m_iconElement.sprite = icon;
        }

        internal static void Remove(Minimap.PinData pin)
        {
            Minimap map = Minimap.instance;
            if (map == null || pin == null) return;

            map.RemovePin(pin);
        }
    }
}
