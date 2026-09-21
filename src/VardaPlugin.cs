using System.Runtime.CompilerServices;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using Ezomic.Core;
using HarmonyLib;

namespace Varda
{
    /// <summary>
    /// Varda marks the map with the places you have actually been: a dungeon pins itself when
    /// you go in, and a portal you built pins itself with the tag you gave it.
    ///
    /// A varda is a cairn - a stack of stones somebody left at the side of a route so the next
    /// person could find it again. That is the whole design rule, and it is a rule about what
    /// the mod refuses to do. Every map mod eventually arrives at pinning ore, and ore pinning
    /// is a detector: it tells you where something is that you have not found, and it turns
    /// the part of the game that is exploration into the part that is reading a list. What
    /// Varda pins is memory. You walked into that crypt at dusk and came out the far side of
    /// the mountain; you built that portal and tagged it; neither pin tells you anything you
    /// did not already know. It saves you writing it down, and nothing else.
    ///
    /// The pins are ordinary vanilla pins - you can rename them, tick them off, filter them
    /// and delete them, and they are written into the map file with all the others. The mod
    /// only paints its own picture on top. That is deliberate: a custom pin type would be
    /// dropped on load by anyone without the mod, silently taking their pins with it.
    ///
    /// Client-side in the strict sense: every effect is computed by the owning client off
    /// state it already has, and nothing is written anywhere but this machine. A player
    /// without Varda sees exactly the game they would have seen, which is why
    /// Requirement.HostOnly below is correct rather than merely permissive.
    ///
    /// There is deliberately no BepInProcess attribute. A dedicated server runs
    /// valheim_server.exe, and Core's gate only refuses on the server side of RPC_PeerInfo -
    /// so a mod that must be enforced has to be allowed to load there.
    /// </summary>
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    // Soft, not hard. A hard dependency that is absent does not degrade - the plugin never
    // loads at all - and every mod here has to be installable on its own, because a stranger
    // should not need two installs to get one mod. Soft still buys the load-order guarantee
    // when Core is present, which is all that registering with the gate needs.
    [BepInDependency(CoreGuid, BepInDependency.DependencyFlags.SoftDependency)]
    public class VardaPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "ezomic.valheim.varda";
        public const string PluginName = "Varda";
        public const string PluginVersion = "0.1.0";
        public const string PluginAuthor = "Robbin Thijssen";

        /// <summary>Core's plugin GUID. Optional - see TryRegisterWithCore.</summary>
        private const string CoreGuid = "ezomic.valheim.core";

        internal static ManualLogSource Log;

        /// <summary>
        /// Whether Core answered at load. Worth keeping even when nothing reads it yet: the
        /// difference between gated and ungated is invisible to a player otherwise, and this
        /// is what a warning on spawn would be driven by.
        /// </summary>
        internal static bool CorePresent;

        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;

            // Config first. Registering absorbs every entry the mod has bound, so anything
            // bound after this line is carried only because Core re-absorbs at manifest
            // time - and depending on the order of two lines in an Awake is not a thing
            // worth relying on.
            VardaConfig.Bind(Config);

            TryRegisterWithCore();

            // PatchAll over a named type, never the whole assembly. A bare PatchAll() walks
            // every type in the DLL, so a half-written patch class in another file goes live
            // the moment it compiles.
            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(typeof(VardaPatches));

            // The startup line every mod in the suite writes. It is how a log answers "which
            // build of what is actually loaded" without anyone guessing.
            Log.LogInfo(PluginName + " " + PluginVersion + " by " + PluginAuthor + " - ready.");
        }

        /// <summary>
        /// Joins Core's version gate when Core is installed, and does nothing when it is not.
        ///
        /// Name here exactly what standing alone costs, because it is usually not the mod.
        /// For most of these it is the *enforcement*: without Core nothing refuses a client
        /// that lacks the plugin, so the rule becomes an agreement between players rather
        /// than a property of the server. That is a real loss and a legitimate choice, and
        /// it is the server owner's to make - which is why this logs rather than refusing
        /// to run.
        /// </summary>
        private void TryRegisterWithCore()
        {
            CorePresent = Chainloader.PluginInfos.ContainsKey(CoreGuid);

            if (!CorePresent)
            {
                Log.LogInfo("Core not installed - running standalone, without the version gate.");
                return;
            }

            RegisterWithCore();
        }

        /// <summary>
        /// Kept separate and never inlined on purpose. The JIT resolves the assemblies a
        /// method needs when it first compiles that method, so a Suite call sitting directly
        /// in Awake would drag Ezomic.Core in before the check above could prevent it - and
        /// the missing-assembly exception would land during plugin load, which is the exact
        /// failure this arrangement exists to avoid. Isolating it means the type is only
        /// ever resolved on a machine that has Core.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void RegisterWithCore()
        {
            // HostOnly, and it is the whole of what this mod asks of a server: nothing. It
            // registers no prefab, writes no ZDO and changes no item - a pin lives in the map
            // file on this machine - so a client without it is genuinely unaffected. Core
            // honours that in both directions, which is the half that had to be fixed for
            // Skaft: a server without Varda still lets in a client that has it.
            Suite.Register(PluginGuid, PluginName, PluginVersion, Config, Requirement.HostOnly);

            // Every entry is Local, and that is not caution - Register absorbs the whole file
            // and the host's values are imposed on anything left synced, which here would mean
            // a server deciding what is on your personal map. Vaettir paid for that lesson
            // with a grid angle that turned in singleplayer and refused to turn online:
            // Core's SettingChanged watch puts an imposed value straight back the moment
            // anything writes it.
            Suite.Local(
                VardaConfig.Enabled,
                VardaConfig.Dungeons,
                VardaConfig.Portals,
                VardaConfig.NameDungeons,
                VardaConfig.DungeonPinType,
                VardaConfig.PortalPinType,
                VardaConfig.DungeonIcon,
                VardaConfig.PortalIcon,
                VardaConfig.MergeRadius,
                VardaConfig.DumpIcons,
                VardaConfig.Verbose);
        }

        private void OnDestroy()
        {
            // UnpatchSelf, never UnpatchAll(). The argumentless one unpatches every mod in
            // the process, not just this one.
            if (_harmony != null) _harmony.UnpatchSelf();
        }
    }
}
