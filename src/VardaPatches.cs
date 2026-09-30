using System;
using HarmonyLib;

namespace Varda
{
    /// <summary>
    /// The mod's Harmony patches. One class named in the plugin's PatchAll, so nothing goes
    /// live by being written.
    ///
    /// Eight hooks and no more, which is most of the argument for the design: the game already
    /// has a single method for going into a dungeon, a heartbeat on every loaded portal, two
    /// well-defined moments where a new map and a new character arrive, one place the map is
    /// saved, a portal's own hover text, and the console's table of commands. Nothing here
    /// patches movement, the map's drawing, or the pin system itself.
    ///
    /// A destroyed portal needs no patch at all. ZDOMan announces every destroyed ZDO through
    /// its public m_onZDODestroyed callback, which is how ZNetScene itself hears of one, and
    /// Portals.Watch joins it from OnSpawned below.
    /// </summary>
    internal static class VardaPatches
    {
        /// <summary>
        /// Captures where you are standing before the teleport, because afterwards you are
        /// inside - and a dungeon interior sits above y 3000 directly over its own entrance,
        /// so the position read a line later would put a pin three kilometres in the air.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Teleport), nameof(Teleport.Interact))]
        private static void TeleportInteractPrefix(
            Teleport __instance, Humanoid character, out Entrances.Entry __state)
        {
            __state = Entrances.Before(__instance, character);
        }

        /// <summary>
        /// Acts only on a true return, which is the game saying the move actually happened -
        /// a boss-blocked door and an unconnected one both return false and leave no pin.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Teleport), nameof(Teleport.Interact))]
        private static void TeleportInteractPostfix(bool __result, Entrances.Entry __state)
        {
            Entrances.After(__state, __result);
        }

        /// <summary>
        /// The portal's own twice-a-second heartbeat, which is private and invoked by name
        /// from its Awake. Riding it is what lets a pin follow a tag that is typed after the
        /// portal is built, and changed again later, without a second patch.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(TeleportWorld), "UpdatePortal")]
        private static void UpdatePortal(TeleportWorld __instance)
        {
            Portals.Tick(__instance);
        }

        /// <summary>
        /// The line naming HidePortalKey, on a portal of yours. Appended, because the text the
        /// portal returns is already localised and finished, and at the default priority, so
        /// with Skra installed it sits straight under vanilla's "[E] Set tag", where the preview
        /// Robbin picked put it, and Skra's owner line follows it.
        ///
        /// Inside a try, because Hud asks for this text every frame the crosshair is on a
        /// portal, and a throw here would take the portal's whole hover text with it each time.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(TeleportWorld), nameof(TeleportWorld.GetHoverText))]
        private static void PortalHoverText(TeleportWorld __instance, ref string __result)
        {
            try
            {
                if (string.IsNullOrEmpty(__result)) return;

                __result += Hiding.HoverLine(__instance);
            }
            catch (Exception e)
            {
                VardaPlugin.Log.LogWarning(
                    "Could not add the hide line to a portal's hover text (" + e.GetType().Name
                    + ": " + e.Message + ").");
            }
        }

        /// <summary>
        /// Registers `vardatest` once the console's command table has been built. Every Terminal
        /// calls InitTerminal from its Awake, the game's own flag makes all but the first return
        /// at once, and DevConsole.Register keeps a flag of its own for the same reason.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Terminal), "InitTerminal")]
        private static void InitTerminal()
        {
            DevConsole.Register();
        }

        /// <summary>
        /// A new map means new sprites. The borrowed half cannot be cached across worlds: in
        /// 1.0 the soft-ref bundles unload at logout and destroy what was in them, so a sprite
        /// held from the last session is a destroyed object.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Minimap), "Awake")]
        private static void MinimapAwake()
        {
            Icons.Reset();
            IconDump.Reset();
        }

        /// <summary>
        /// Puts Varda's icons back on the pins that came out of the map file.
        ///
        /// On spawn rather than on the map loading, for a plain reason: the remembered-pin
        /// file is named after the world AND the character, and the character does not exist
        /// until this runs. It is idempotent, so a respawn after death costs a re-read of a
        /// small file.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Player), nameof(Player.OnSpawned))]
        private static void OnSpawned(Player __instance)
        {
            // Every player object in the scene runs this, not only yours.
            if (__instance != Player.m_localPlayer) return;
            if (!VardaConfig.Enabled.Value) return;

            // Forces the sprite tables to resolve, which is also what writes them to the log
            // under Verbose - and that log is the only way to find out which pin type wears
            // which picture, because it is asset data.
            Icons.Warm();
            IconDump.Run();

            Remembered.Load();
            Remembered.Apply();

            // After the sidecar, never before: from here on a destroyed portal is looked up in
            // it, and Portals.Sweep treats this call as the sign that the list in memory
            // belongs to the world now loaded.
            Portals.Watch();
        }

        /// <summary>
        /// The first half of the map reaching disk: the pins have just been copied into the
        /// profile. Game.SavePlayerProfile is the only caller, and it skips this whenever it is
        /// not going to save the character at all, which is why the copy is watched here rather
        /// than assumed from SavePlayerProfile having been called. See Remembered.Forget.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Minimap), nameof(Minimap.SaveMapData))]
        private static void SaveMapData()
        {
            Remembered.MapTaken();
        }

        /// <summary>
        /// The second half: that profile has been written. A postfix does not run when the
        /// original throws, so a save that failed loudly lets nothing go.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Game), nameof(Game.SavePlayerProfile))]
        private static void SavePlayerProfile()
        {
            Remembered.MapWritten();
        }
    }
}
