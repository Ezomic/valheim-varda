using UnityEngine;

namespace Varda
{
    /// <summary>
    /// Pins the portals you built, labelled with whatever you tagged them.
    ///
    /// <b>Why it hangs off UpdatePortal.</b> A portal arranges its own heartbeat -
    /// <c>Awake</c> ends in <c>InvokeRepeating("UpdatePortal", 0.5f, 0.5f)</c> - so there is
    /// already a twice-a-second call, per loaded portal, at a point where the ZDO is known
    /// good. Riding it costs nothing and solves two problems that a one-shot hook on Awake or
    /// on placement would each have: the tag is typed *after* the portal is built, and it can
    /// be changed at any time afterwards. Watching the portal instead of watching the moment
    /// means the pin follows the tag without a second patch.
    ///
    /// <b>Why yours only.</b> Portals are shared. Pinning every portal the world streams past
    /// you would put other people's network on your map without them choosing it, which is
    /// somebody else's information rather than a record of where you have been.
    /// <c>Piece.GetCreator()</c> against your own player id is the test, and it is the same
    /// number the game writes when the piece is placed.
    /// </summary>
    internal static class Portals
    {
        internal static void Tick(TeleportWorld portal)
        {
            if (!VardaConfig.Enabled.Value || !VardaConfig.Portals.Value) return;
            if (portal == null) return;

            Player player = Player.m_localPlayer;
            if (player == null || Minimap.instance == null) return;

            Piece piece;
            if (!portal.TryGetComponent(out piece)) return;

            // A creator of 0 is a portal nobody owns - one placed by the world, or by a build
            // that never recorded it. Not ours, and not something to claim.
            long creator = piece.GetCreator();
            if (creator == 0L || creator != player.GetPlayerID()) return;

            Vector3 pos = portal.transform.position;
            float radius = VardaConfig.MergeRadius.Value;
            string tag = portal.GetText();
            if (tag == null) tag = "";

            Minimap.PinData existing = Pins.Near(pos, radius);

            if (existing == null)
            {
                Minimap.PinData pin = Pins.Add(
                    pos, VardaConfig.PortalPinType.Value, tag, Icons.Portal());

                if (pin == null) return;

                Remembered.Note(pos, Kind.Portal);

                if (VardaConfig.Verbose.Value)
                {
                    VardaPlugin.Log.LogInfo(
                        "Pinned your portal at " + pos
                        + (tag.Length == 0 ? " (no tag yet)" : " as \"" + tag + "\""));
                }

                return;
            }

            // Something is already here. Only ever touch it if it is one of ours: a pin the
            // player placed on their own portal by hand is theirs, and renaming it out from
            // under them is the kind of thing that makes a mod feel like it is fighting you.
            if (!Remembered.Holds(pos, radius)) return;

            if (existing.m_name == tag) return;

            // Renaming in place is not enough. AddPin builds a PinNameData for any pin with a
            // name, and it is that object which draws the label on the large map - writing
            // m_name afterwards changes the pin and not the thing on the screen. Replacing the
            // pin costs nothing and goes through the same path as a fresh one.
            Pins.Remove(existing);

            Minimap.PinData renamed = Pins.Add(
                pos, VardaConfig.PortalPinType.Value, tag, Icons.Portal());

            if (renamed == null) return;

            if (VardaConfig.Verbose.Value)
            {
                VardaPlugin.Log.LogInfo("Renamed a portal pin to \"" + tag + "\"");
            }
        }
    }
}
