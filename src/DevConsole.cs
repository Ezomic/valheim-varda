using System.Globalization;
using UnityEngine;

namespace Varda
{
    /// <summary>
    /// `vardatest`, a console command for scenarios. The gesture HidePortalKey adds is a key
    /// pressed while looking at a portal, and no Devkit step can press a key, so this is how a
    /// scenario reaches it.
    ///
    ///   vardatest              what Varda remembers here, and the state of the nearest portal
    ///   vardatest hide         hide the nearest portal of yours within five metres
    ///   vardatest show         show it again
    ///   vardatest tag &lt;text&gt;   tag it
    ///
    /// <b>hide and show call Hiding.Set</b>, the method the key calls once it has found the
    /// portal under the crosshair, so everything after the keystroke is what they test: the
    /// record, the pin coming off, the message, and the heartbeat that must not put it back.
    /// What they cannot test is the keystroke itself, the walk from the crosshair to the portal,
    /// and the refusals while a window or a text box has the keyboard. Those are written down in
    /// the scenario as things only a person can check.
    ///
    /// <b>tag exists for one assertion.</b> "Show brings the pin back with its tag" says nothing
    /// while every portal is untagged, and no step can type into the tag box. It calls
    /// TeleportWorld.SetText, which is what the tag box calls when you press OK.
    ///
    /// <b>A cheat, registered isCheat.</b> hide and show only do what the key does, but tag
    /// writes a portal's tag from a few metres off without the ward check the tag box sits
    /// behind, and the cheat flag belongs to a whole command. Devkit's `mod` step calls the
    /// handler directly, past RunAction, so a scenario runs it without devcommands and without
    /// the cheat mark on the character. Utangard's `utangardtest` is registered the same way for
    /// the same reason.
    ///
    /// Failable, so a refusal comes back as a string and fails the step rather than reading as
    /// having run. The output is name=value tokens with no spaces inside, because Devkit's
    /// `printed` step matches a substring.
    /// </summary>
    internal static class DevConsole
    {
        /// <summary>
        /// How far the verbs look for a portal of yours, flat. Far enough for the portal a
        /// scenario put three metres ahead, and short of the one it put beside that, which is
        /// what lets a scenario choose between two portals by where the player stands.
        /// </summary>
        private const float Reach = 5f;

        /// <summary>
        /// Process-wide, not per world. Terminal's command table is a private static that
        /// nothing clears, so a second registration would leave a duplicate behind.
        /// </summary>
        private static bool _registered;

        /// <summary>From VardaPatches, after Terminal.InitTerminal.</summary>
        internal static void Register()
        {
            if (_registered) return;
            _registered = true;

            new Terminal.ConsoleCommand("vardatest",
                "vardatest [hide | show | tag <text>] - for tests: what Varda remembers here, or "
                + "hide, show or tag the nearest portal of yours within 5 metres",
                new Terminal.ConsoleEventFailable(OnTest), isCheat: true);
        }

        private static object OnTest(Terminal.ConsoleEventArgs args)
        {
            Terminal term = args.Context;
            if (term == null) return "no console to answer in";

            Player player = Player.m_localPlayer;
            if (player == null || ZNetScene.instance == null) return "no world loaded";

            string verb = args.Length > 1 ? args[1].ToLowerInvariant() : "";

            if (verb.Length == 0)
            {
                Report(term, player);
                return true;
            }

            if (verb != "hide" && verb != "show" && verb != "tag")
            {
                return "unknown '" + args[1] + "'. Try vardatest, vardatest hide, vardatest show "
                       + "or vardatest tag <text>.";
            }

            if (!VardaConfig.Enabled.Value || !VardaConfig.Portals.Value)
            {
                return "Varda's Enabled or Portals is off, and the key does nothing then either";
            }

            TeleportWorld portal = Nearest(player);
            if (portal == null) return "no portal of yours within " + Reach + " metres";

            string at = Where(portal.transform.position);

            if (verb == "tag")
            {
                if (args.Length < 3) return "vardatest tag <text>";

                string text = string.Join(" ", args.Args, 2, args.Length - 2);
                portal.SetText(text);

                term.AddString("vardatest tag: tagged=\"" + text + "\" at=" + at
                               + "   (its pin follows on the portal's next heartbeat)");
                return true;
            }

            bool hide = verb == "hide";
            bool was = Hiding.Set(portal, hide);

            term.AddString("vardatest " + verb + ": hidden=" + (hide ? "yes" : "no")
                           + " was=" + (was ? "yes" : "no") + " at=" + at);
            return true;
        }

        /// <summary>
        /// Counts from the sidecar as this session holds it, and the nearest portal of yours.
        /// portalpins counts the pins Varda is keeping track of, not the ones on the map, which
        /// `pins` in Devkit already counts; hiddenportals is the number a destroyed portal has
        /// to bring down.
        /// </summary>
        private static void Report(Terminal term, Player player)
        {
            string line = "vardatest portalpins=" + Remembered.Of(Remembered.Portal).Count
                          + " hiddenportals=" + Remembered.Of(Remembered.HiddenPortal).Count;

            TeleportWorld portal = Nearest(player);
            if (portal == null)
            {
                term.AddString(line + " nearest=none");
                return;
            }

            Vector3 pos = portal.transform.position;

            term.AddString(line
                           + " nearest=" + (Hiding.IsHidden(pos) ? "hidden" : "shown")
                           + " pin=" + (Pins.At(pos) != null && Remembered.Has(pos, Remembered.Portal) ? "yes" : "no")
                           + " at=" + Where(pos));
        }

        /// <summary>
        /// The nearest portal you built within <see cref="Reach"/>, flat, by the same test Tick
        /// pins by. The whole scene is searched, which is fine for a command and would not be
        /// for something run every frame.
        /// </summary>
        private static TeleportWorld Nearest(Player player)
        {
            Vector3 here = player.transform.position;
            TeleportWorld best = null;
            float closest = Reach;

            foreach (TeleportWorld portal in Object.FindObjectsByType<TeleportWorld>(FindObjectsSortMode.None))
            {
                if (!Portals.Yours(portal, player)) continue;

                float distance = Utils.DistanceXZ(here, portal.transform.position);
                if (distance >= closest) continue;

                closest = distance;
                best = portal;
            }

            return best;
        }

        private static string Where(Vector3 pos)
        {
            return pos.x.ToString("0.0", CultureInfo.InvariantCulture) + ","
                   + pos.z.ToString("0.0", CultureInfo.InvariantCulture);
        }
    }
}
