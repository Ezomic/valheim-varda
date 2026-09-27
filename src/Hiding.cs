using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Varda
{
    /// <summary>
    /// One portal's pin, off your map or back on it: look at a portal you built and press
    /// HidePortalKey. The portal's hover text gains a line in the game's own form saying which
    /// the key will do, "[H] Hide from your map" or "[H] Show on your map".
    ///
    /// <b>Why a key on the portal and not a setting.</b> Robbin asked for it per portal and in
    /// the game, and picked this from a preview. A list in the cfg would need you to know a
    /// portal's coordinates, and a switch would take every portal at once, which Portals already
    /// does.
    ///
    /// <b>Why not Shift+E.</b> It is the gesture the game would suggest for "something else you
    /// can do to this", and Skra already puts its portal settings there. One press would do both.
    ///
    /// <b>What hidden is.</b> A record of its own kind in the sidecar, on the portal's own spot,
    /// the same way LHM-34 keys a portal's pin. Tick finds it first and leaves the portal without
    /// a pin, and nothing that reads pin records can take it for one. It belongs to this
    /// character in this world, and nothing leaves this machine: the portal is untouched and
    /// nobody else's map changes. A pin you placed by hand is never touched, because the only
    /// pin this ever takes off is the one the sidecar says Varda put on that very spot.
    ///
    /// <b>It goes with the portal.</b> Both moments that take a destroyed portal's pin off forget
    /// its hidden record as well (Portals.ForgetHidden), so a portal built later on the same spot
    /// starts pinned. Only while RemoveDestroyedPortals is on, since both moments answer to it;
    /// with it off the record outlives the portal, as the cfg and the README say.
    ///
    /// <b>The key set to None does not show anything again.</b> It takes away the key and the
    /// hover line, and Tick still reads the records, so a portal hidden before stays hidden with
    /// nothing on it to say why. That is written in the cfg and the README rather than changed
    /// here, because whether None should also bring those pins back is Robbin's call.
    /// </summary>
    internal static class Hiding
    {
        private const string HideWords = "Hide from your map";
        private const string ShowWords = "Show on your map";

        /// <summary>
        /// Player.TakeInput, bound on first use inside a try/catch rather than in a static
        /// initialiser, so a rename in a game update costs the key and its hover line and
        /// nothing else. See <see cref="TakesInput"/> for why this gate and not a list of windows.
        /// </summary>
        private static MethodInfo _takeInput;
        private static bool _triedTakeInput;

        internal static bool IsHidden(Vector3 pos)
        {
            return Remembered.Has(pos, Remembered.HiddenPortal);
        }

        /// <summary>
        /// The key, from the plugin's Update. The press is read first because it is the cheap
        /// question and nearly always no; everything after it runs on the one frame the key
        /// went down.
        /// </summary>
        internal static void Listen()
        {
            if (!VardaConfig.Enabled.Value || !VardaConfig.Portals.Value) return;
            if (!Pressed(VardaConfig.HidePortalKey.Value)) return;

            Player player = Player.m_localPlayer;
            if (player == null || !Listening(player)) return;

            // The hover object is whichever collider the crosshair ray hit, which on a portal is
            // a child of the object carrying TeleportWorld, so it is found by walking up. That is
            // how Player.Interact finds what E is meant for, and how Hud finds the Hoverable whose
            // text carries our line, so the key acts on the portal the line was shown on.
            GameObject hover = player.GetHoverObject();
            if (hover == null) return;

            TeleportWorld portal = hover.GetComponentInParent<TeleportWorld>();
            if (!Portals.Yours(portal, player)) return;

            Set(portal, !IsHidden(portal.transform.position));
        }

        /// <summary>
        /// Hides or shows one portal of yours, and says so in the middle of the screen. The one
        /// place both happen: the key comes here through Listen, and so does `vardatest`, which is
        /// how a scenario reaches it without a key. True when the portal was hidden before.
        ///
        /// Hiding takes the pin off at once. Showing only drops the record, and the portal's own
        /// heartbeat pins it again within half a second with whatever its tag is then, through
        /// exactly the path that pinned it the first time, so a shown portal cannot come back
        /// looking any different from one that was never hidden.
        /// </summary>
        internal static bool Set(TeleportWorld portal, bool hide)
        {
            Vector3 pos = portal.transform.position;
            bool was = IsHidden(pos);

            if (hide)
            {
                if (!was) Remembered.Note(pos, Remembered.HiddenPortal);

                Minimap.PinData taken = TakeOff(pos);

                if (VardaConfig.Verbose.Value)
                {
                    VardaPlugin.Log.LogInfo(
                        "Hid your portal at " + pos + " from your map"
                        + (taken == null
                            ? "; it had no pin of Varda's on it to take off."
                            : " and took its pin"
                              + (string.IsNullOrEmpty(taken.m_name) ? "" : " \"" + taken.m_name + "\"")
                              + " off."));
                }
            }
            else
            {
                Remembered.Drop(pos, Remembered.HiddenPortal);

                if (VardaConfig.Verbose.Value)
                {
                    VardaPlugin.Log.LogInfo(
                        "Showed your portal at " + pos + " on your map again. Its pin comes back "
                        + "on the portal's next heartbeat.");
                }
            }

            Player player = Player.m_localPlayer;
            if (player != null)
            {
                player.Message(MessageHud.MessageType.Center,
                    hide ? "Portal hidden from your map" : "Portal shown on your map");
            }

            return was;
        }

        /// <summary>
        /// From Tick, twice a second, for every portal of yours that you have hidden. Nearly
        /// always nothing to do, which is why it asks the cheap question first.
        ///
        /// What it is there for: hiding forgets the pin at once but the map on disk keeps it until
        /// the game next saves the character, so a crash in between brings the pin back on the
        /// next load, still named in the sidecar as Varda's. This takes it off again.
        /// </summary>
        internal static void KeepOff(Vector3 pos)
        {
            if (!Remembered.Has(pos, Remembered.Portal)) return;

            Minimap.PinData taken = TakeOff(pos);
            if (taken == null || !VardaConfig.Verbose.Value) return;

            VardaPlugin.Log.LogInfo(
                "Took the pin off your hidden portal at " + pos + " again. It came back with a "
                + "map saved before you hid the portal.");
        }

        /// <summary>
        /// Varda's pin on this portal's spot, off the map and out of the pin records. Null when
        /// there was none to take.
        ///
        /// At, on the remembered spot, never Near, for the reason Resolve gives: beside a portal
        /// the nearest pin can be one you put there by hand. The record goes through Forget, not
        /// Drop, because this one does stand for a pin, and the map on disk still has it.
        /// </summary>
        private static Minimap.PinData TakeOff(Vector3 pos)
        {
            Minimap.PinData taken = null;

            foreach (Vector3 spot in Remembered.Of(Remembered.Portal))
            {
                if (Utils.DistanceXZ(spot, pos) >= Pins.SameSpot) continue;

                Minimap.PinData pin = Pins.At(spot);
                if (pin != null)
                {
                    Pins.Remove(pin);
                    taken = pin;
                }

                Remembered.Forget(spot, Remembered.Portal);
            }

            return taken;
        }

        /// <summary>
        /// The line a portal of yours adds to its hover text, or nothing.
        ///
        /// Offered only where pressing the key would work: a key is bound, the gate it is read
        /// through was found, and the portal is one Varda would pin. A hint for a key that then
        /// does nothing is worse than no hint, and anybody else's portal is not yours to hide.
        /// </summary>
        internal static string HoverLine(TeleportWorld portal)
        {
            if (!VardaConfig.Enabled.Value || !VardaConfig.Portals.Value) return "";

            KeyCode key = VardaConfig.HidePortalKey.Value;
            if (key == KeyCode.None || !Bound()) return "";

            if (!Portals.Yours(portal, Player.m_localPlayer)) return "";

            // Vanilla's own form for a key prompt, the one the portal's "[E] Set tag" line above
            // it uses: the key yellow and bold in square brackets, then what it does.
            return "\n[<color=yellow><b>" + KeyName(key) + "</b></color>] "
                   + (IsHidden(portal.transform.position) ? ShowWords : HideWords);
        }

        /// <summary>
        /// The key as the game would print it.
        ///
        /// Not a $KEY_ token, since those name the game's own bindings and this is not one. And
        /// not the KeyCode's own name either: ZInput reads a KeyCode as a physical key, so on an
        /// AZERTY keyboard the key it hears for KeyCode.A is the one printed Q, and naming the
        /// KeyCode would name the wrong key. ZInput.KeyCodeToDisplayName asks the input system
        /// for that key's display name, which follows the keyboard layout.
        /// </summary>
        private static string KeyName(KeyCode key)
        {
            string name = null;

            try
            {
                name = ZInput.KeyCodeToDisplayName(key);
            }
            catch (Exception)
            {
                // The KeyCode's own name below is a worse answer and still a readable one.
            }

            // A key the input system has no control for comes back as a sentence starting
            // "$KeyCode", and anything downstream that localises the hover text would take that
            // for a token.
            if (string.IsNullOrEmpty(name) || name.StartsWith("$", StringComparison.Ordinal))
            {
                return key.ToString();
            }

            return name;
        }

        /// <summary>
        /// Down this frame, and not typed into a text field. The same manners as Vaettir's
        /// Keys.Pressed: ZInput rather than UnityEngine.Input, because Valheim runs on the new
        /// Input System and the legacy class misses some keys entirely, and logWarning false,
        /// because a key nobody bound is a configuration choice rather than a fault.
        /// </summary>
        private static bool Pressed(KeyCode key)
        {
            if (key == KeyCode.None) return false;
            if (!ZInput.GetKeyDown(key, false)) return false;

            if (Chat.instance != null && Chat.instance.HasFocus()) return false;
            return !Console.IsVisible() && !TextInput.IsVisible();
        }

        /// <summary>
        /// Whether a keystroke right now is meant for the player: the game is not paused, the
        /// radial menu is not open (vanilla's own Use key refuses there too), and the game itself
        /// would take input.
        /// </summary>
        private static bool Listening(Player player)
        {
            if (Game.IsPaused() || Hud.InRadial()) return false;

            return TakesInput(player);
        }

        /// <summary>
        /// Player.TakeInput: the game's own answer to "is this keystroke for the player".
        ///
        /// It is false while the player is dead, teleporting or in a cutscene, and while chat,
        /// the console, a text box such as the portal's own tag box, the inventory, a trader,
        /// the menu, a book, the large map or the build menu's search field has the keyboard. It
        /// is also the gate every mod here shuts while its own window is open: Devkit, Skra, Rist,
        /// Stow and Thralls all postfix it. So an H typed into any of those windows is not a
        /// keystroke for Varda, without Varda knowing any of them exist, which a hand-kept list
        /// of windows could never manage.
        ///
        /// Protected, so it is reached by reflection, and invoked only on the frame the key went
        /// down.
        /// </summary>
        private static bool TakesInput(Player player)
        {
            if (!Bound()) return false;

            try
            {
                return (bool)_takeInput.Invoke(player, null);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool Bound()
        {
            if (_triedTakeInput) return _takeInput != null;
            _triedTakeInput = true;

            try
            {
                _takeInput = AccessTools.Method(typeof(Player), "TakeInput", Type.EmptyTypes);
            }
            catch (Exception)
            {
                _takeInput = null;
            }

            if (_takeInput != null && _takeInput.ReturnType == typeof(bool)) return true;

            _takeInput = null;
            VardaPlugin.Log.LogWarning(
                "Cannot find Player.TakeInput, the game's own test for whether a keystroke is "
                + "meant for the player. HidePortalKey does nothing and portals do not offer it; "
                + "pins are unaffected.");

            return false;
        }
    }
}
