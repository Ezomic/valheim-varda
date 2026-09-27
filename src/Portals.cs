using System;
using System.Collections.Generic;
using UnityEngine;

namespace Varda
{
    /// <summary>
    /// Pins the portals you built, labelled with whatever you tagged them, and takes the pin off
    /// again when the portal is gone.
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
    ///
    /// <b>One pin per portal, on the portal's own position.</b> A portal only ever touches the
    /// pin the sidecar remembers on its very spot. The first version let two of your portals
    /// inside MergeRadius share one pin, found as the nearest pin to either of them, and with
    /// two different tags each heartbeat renamed it to its own: a row of tagged portals, which
    /// is how most people build them, showed one pin changing its name twice a second for as
    /// long as they stood in view. The nearest pin was also how a portal came to rename a
    /// dungeon pin, or dress one the player had put down by hand. Matching by the remembered
    /// spot ends all three, and it makes a destroyed portal simple: its pin is the one on its
    /// spot, and no neighbour has any say in it.
    ///
    /// <b>Why "destroyed" is read off the ZDO and never off the object.</b> A portal's object
    /// disappears in two situations that look the same from the object's side: the portal is
    /// destroyed, or you walked far enough away that <c>ZNetScene.RemoveObjects</c> unloaded
    /// it. Only the ZDO tells them apart. Removing or breaking a piece ends in
    /// <c>ZNetScene.Destroy</c>, which hands the ZDO to <c>ZDOMan.DestroyZDO</c>, and that is
    /// announced to every machine holding it as <c>HandleDestroyedZDO</c> and the
    /// <c>m_onZDODestroyed</c> callback. An unload destroys the object and leaves the ZDO
    /// alone, because RemoveObjects only destroys the ZDOs that are not persistent and a
    /// built piece is.
    ///
    /// <b>Two moments, because the pin is on one player's map and the portal can go while that
    /// player is somewhere else.</b> If this machine holds the portal's ZDO when it goes, the
    /// callback says so and the pin comes off at once (see <see cref="Destroyed"/>). If it does
    /// not, because you were on the other side of the world or not playing, nothing is ever
    /// sent here, and the only evidence left is absence: the next time you stand by the pin,
    /// once the area has settled, no portal of yours is there. <see cref="Sweep"/> is that
    /// second half.
    ///
    /// <b>Only while Portals is on.</b> A removal that rests on judging an area can be wrong,
    /// and what puts a wrongly removed pin back is Tick, which stops when Portals is off. A
    /// removal at the moment of destruction cannot be wrong that way and it stops as well, so
    /// that the switch means one thing: off, Varda leaves portal pins alone entirely.
    /// </summary>
    internal static class Portals
    {
        /// <summary>
        /// How long a pin's spot has to have been loaded and ready, without a break, before an
        /// empty spot is believed.
        ///
        /// The game's own answer to "has everything here arrived yet" is a wait. Player's
        /// UpdateTeleport will not end a distant teleport before eight seconds have passed AND
        /// IsAreaReady holds, however much it has already received, because nothing tells a
        /// client that the server has finished sending an area. This asks for the same thing
        /// with room over it. Nothing here is in a hurry: a portal destroyed while this machine
        /// held it has been handled by <see cref="Destroyed"/> already, so what is left is a pin
        /// that has been wrong since before you arrived, and ten seconds more of that costs
        /// nothing. Taking the pin off a portal that still stands would cost a great deal more.
        /// </summary>
        private const float SettleSeconds = 10f;

        /// <summary>How often <see cref="Sweep"/> looks. Once a second is plenty for a wait of ten.</summary>
        private const float SweepEvery = 1f;

        /// <summary>
        /// The ZDOMan being listened to. A new one is built for every world, so this is also how
        /// Sweep knows the sidecar in memory belongs to the world now loaded. See Watch.
        /// </summary>
        private static ZDOMan _watched;

        private static float _nextSweep;

        /// <summary>When each remembered portal spot last became settled, by its flat position.</summary>
        private static readonly Dictionary<Vector2, float> SettledSince = new Dictionary<Vector2, float>();

        private static readonly HashSet<Vector2> SettledNow = new HashSet<Vector2>();
        private static readonly List<Vector2> NoLongerSettled = new List<Vector2>();
        private static readonly List<ZDO> Nearby = new List<ZDO>();

        /// <summary>
        /// The pins Varda put on portals, which Tick passes over when it asks whether a pin
        /// already sits on a portal. Those belong to the portals they are on, so a row of portals
        /// side by side each keeps its own. One delegate for the life of the process, because
        /// Tick asks twice a second for every portal of yours that is loaded.
        /// </summary>
        private static readonly Predicate<Minimap.PinData> PortalPin =
            pin => Remembered.Has(pin.m_pos, Remembered.Portal);

        private const string DestroyedHere = "was destroyed while your game had it loaded";

        // Not "when you came back". A portal broken while RemoveDestroyedPortals was off is found
        // this way too, once the switch is back on, by a player who may never have left.
        private const string FoundGone = "was not there once the area around it had settled";

        internal static void Tick(TeleportWorld portal)
        {
            if (!VardaConfig.Enabled.Value || !VardaConfig.Portals.Value) return;
            if (portal == null) return;

            Player player = Player.m_localPlayer;
            if (player == null || Minimap.instance == null) return;

            // A portal whose ZNetView has let go of its ZDO is on its way out. ZNetScene resets
            // the view and leaves the object itself to be destroyed at the end of the frame, on
            // a destroy and on an unload alike, and the heartbeat can still land in that gap.
            // Pinning it then would put back the very pin its destruction just took off.
            ZNetView nview;
            if (!portal.TryGetComponent(out nview) || !nview.IsValid()) return;

            Piece piece;
            if (!portal.TryGetComponent(out piece)) return;

            // A creator of 0 is a portal nobody owns - one placed by the world, or by a build
            // that never recorded it. Not ours, and not something to claim.
            long creator = piece.GetCreator();
            if (creator == 0L || creator != player.GetPlayerID()) return;

            Vector3 pos = portal.transform.position;
            string tag = portal.GetText();
            if (tag == null) tag = "";

            bool remembered = Remembered.Has(pos, Remembered.Portal);

            if (remembered)
            {
                Minimap.PinData own = Pins.At(pos);

                if (own != null)
                {
                    if (own.m_name != tag) Rename(own, tag);
                    return;
                }

                // Remembered and gone from the map means it was deleted by hand. It is pinned
                // again below, as it always has been while the portal stands, unless a pin of
                // the player's now sits on the portal instead, which the check below respects.
            }

            // Something already on this portal that is not one of Varda's portal pins: most
            // likely a pin you placed on it by hand, possibly one from a cartography table. It
            // stands for the portal, whoever put it there, and a second pin on top of it would be
            // the doubling this check exists to stop. Varda's pins on your other portals are
            // passed over, which is what lets portals side by side keep a pin each, and the
            // radius is kept small for the same reason: see PortalMergeRadius.
            float radius = VardaConfig.PortalMergeRadius.Value;
            if (Pins.Near(pos, radius, PortalPin) != null) return;

            Minimap.PinData pin = Pins.Add(pos, VardaConfig.PortalPinType.Value, tag, Icons.Portal());
            if (pin == null) return;

            // Only when it is new. A pin put back after a delete already has its line, and a
            // second one on the same spot would only have to be forgotten twice.
            if (!remembered) Remembered.Note(pos, Remembered.Portal);

            if (VardaConfig.Verbose.Value)
            {
                VardaPlugin.Log.LogInfo(
                    "Pinned your portal at " + pos
                    + (tag.Length == 0 ? " (no tag yet)" : " as \"" + tag + "\""));
            }
        }

        /// <summary>
        /// Gives this portal's own pin its new tag. Only ever reached with the pin found by
        /// Pins.At on the portal's remembered spot, so it cannot be anybody else's.
        /// </summary>
        private static void Rename(Minimap.PinData own, string tag)
        {
            // Renaming in place is not enough. AddPin builds a PinNameData for any pin with a
            // name, and it is that object which draws the label on the large map - writing
            // m_name afterwards changes the pin and not the thing on the screen. Replacing the
            // pin costs nothing and goes through the same path as a fresh one.
            //
            // At the pin's own position rather than the portal's. They are the same point to
            // within float noise, and the pin's numbers are the very ones the sidecar holds, so
            // this is the one choice that can never move a pin off the spot it is found by.
            Pins.Remove(own);

            Minimap.PinData renamed = Pins.Add(
                own.m_pos, VardaConfig.PortalPinType.Value, tag, Icons.Portal());

            if (renamed == null) return;

            if (VardaConfig.Verbose.Value)
            {
                VardaPlugin.Log.LogInfo("Renamed a portal pin to \"" + tag + "\"");
            }
        }

        /// <summary>
        /// Starts listening for destroyed ZDOs in the world now loaded. From OnSpawned, straight
        /// after the sidecar has been read, and idempotent, so a respawn after death adds
        /// nothing.
        ///
        /// The game's own callback rather than a patch. <c>m_onZDODestroyed</c> is public and it
        /// is how ZNetScene itself learns that an object has to go, so it fires for exactly the
        /// destroys that matter and for no unload. It has to be joined per world, because
        /// ZNet builds a fresh ZDOMan every time one is loaded.
        /// </summary>
        internal static void Watch()
        {
            ZDOMan zdoMan = ZDOMan.instance;
            if (zdoMan == null || zdoMan == _watched) return;

            zdoMan.m_onZDODestroyed += Destroyed;
            _watched = zdoMan;
            SettledSince.Clear();
        }

        /// <summary>
        /// A ZDO this machine was holding has been destroyed, wherever it was and whoever did
        /// it. Every destroyed ZDO this machine holds comes through here, which is why the first
        /// test is the one that throws nearly all of them out.
        /// </summary>
        private static void Destroyed(ZDO zdo)
        {
            // All of it inside a try. This runs in the middle of ZDOMan.HandleDestroyedZDO,
            // before the lines that take the ZDO out of its sector and give it back to the pool,
            // and RPC_DestroyZDO calls that once per ZDO in a batch. An exception escaping here
            // would leave the game holding a ZDO it believes it has destroyed, and would drop the
            // rest of the batch on the floor. A pin left on the map is the most this may cost.
            try
            {
                if (!Active()) return;
                if (zdo == null) return;

                Player player = Player.m_localPlayer;
                if (player == null || Minimap.instance == null) return;

                long me = player.GetPlayerID();
                if (!IsYourPortal(zdo, me)) return;

                // At once, on the host and on a client alike, because the pin on this portal's
                // spot is this portal's and nothing else has to be asked. A client used to hand
                // most destroys to Sweep instead: while two portals could share a pin it had to
                // know whether the other one stood, and a client can hold a portal from anywhere
                // on the map without its neighbours, since Game.SetConnection force-sends a
                // portal to every peer however far away.
                Vector3 where = zdo.GetPosition();

                foreach (Vector3 spot in Remembered.Of(Remembered.Portal))
                {
                    if (Utils.DistanceXZ(spot, where) >= Pins.SameSpot) continue;

                    Resolve(spot, me, zdo, DestroyedHere);
                }
            }
            catch (Exception e)
            {
                VardaPlugin.Log.LogWarning(
                    "Could not check a destroyed object for a portal pin (" + e.GetType().Name
                    + ": " + e.Message + "). The pin, if there was one, stays on the map.");
            }
        }

        /// <summary>
        /// Takes the pin off a remembered portal spot and forgets the spot, unless a portal of
        /// yours still stands on it. The one place either moment ends up.
        /// </summary>
        private static void Resolve(Vector3 spot, long me, ZDO dying, string why)
        {
            // Nearly always no from the destroy, where it only guards against Stands being unable
            // to look. From the sweep it is the whole question, and it is asked every second while
            // you stand beside a portal that is perfectly fine, which is why it says nothing.
            if (Stands(spot, me, dying)) return;

            // At, never Near. Near answers with whichever pin is first inside a radius, and beside
            // a portal that can be one the player put there by hand.
            Minimap.PinData pin = Pins.At(spot);
            if (pin != null) Pins.Remove(pin);

            if (!Remembered.Forget(spot, Remembered.Portal)) return;

            if (!VardaConfig.Verbose.Value) return;

            if (pin != null)
            {
                VardaPlugin.Log.LogInfo(
                    "Took your portal pin"
                    + (string.IsNullOrEmpty(pin.m_name) ? "" : " \"" + pin.m_name + "\"")
                    + " off the map at " + spot + ": the portal " + why
                    + ". Varda no longer remembers it.");
            }
            else
            {
                VardaPlugin.Log.LogInfo(
                    "Forgot your portal at " + spot + ": the portal " + why
                    + ", and its pin had already been taken off by hand.");
            }
        }

        /// <summary>
        /// A portal you built: your id in the creator key, and a prefab carrying TeleportWorld.
        ///
        /// Creator first, because it is one lookup and it rules out arrows, drops, dead
        /// creatures and everybody else's building, which is nearly every ZDO the game destroys.
        /// Then TeleportWorld, the component Tick hangs off, rather than Game's list of portal
        /// prefabs: that list is what the game connects, and this has to match exactly what
        /// Varda pinned in the first place.
        /// </summary>
        private static bool IsYourPortal(ZDO zdo, long me)
        {
            long creator = zdo.GetLong(ZDOVars.s_creator, 0L);
            if (creator == 0L || creator != me) return false;

            ZNetScene scene = ZNetScene.instance;
            if (scene == null) return false;

            GameObject prefab = scene.GetPrefab(zdo.GetPrefab());
            if (prefab == null) return false;

            TeleportWorld portal;
            return prefab.TryGetComponent(out portal);
        }

        /// <summary>
        /// Whether a portal of yours stands on a remembered spot, going by the ZDOs this machine
        /// holds. <paramref name="dying"/> is left out: while its own destruction is being
        /// announced it is still in the sector lists.
        ///
        /// On the spot, to within <see cref="Pins.SameSpot"/>, and not anywhere near it. A pin is
        /// put on its portal's own position, which is the portal's ZDO position to the last bit,
        /// so a portal of yours a few metres off is a different portal with a pin of its own, and
        /// it has no say in this one.
        ///
        /// Says yes when it cannot look, because every caller treats yes as "leave the pin".
        /// </summary>
        private static bool Stands(Vector3 spot, long me, ZDO dying)
        {
            ZDOMan zdoMan = ZDOMan.instance;
            if (zdoMan == null || ZoneSystem.instance == null) return true;

            // The spot's own zone and one ring round it, classic so the ring is a full square.
            // The portal sits in the spot's zone; the ring is there for a spot on a zone's very
            // edge, and costs nothing. FindSectorObjects also reads the portal list ZDOMan keeps
            // apart from the sectors, which is where a vanilla portal's ZDO actually lives.
            Nearby.Clear();
            zdoMan.FindSectorObjects(
                ZoneSystem.GetZone(spot), new SimulationDistance(1, 0, classic: true), Nearby);

            foreach (ZDO zdo in Nearby)
            {
                if (zdo == null || zdo == dying) continue;
                if (Utils.DistanceXZ(zdo.GetPosition(), spot) >= Pins.SameSpot) continue;
                if (IsYourPortal(zdo, me)) return true;
            }

            return false;
        }

        /// <summary>
        /// The second moment: a Varda portal pin whose portal went while this machine was not
        /// holding it. From the plugin's Update, throttled to once a second.
        ///
        /// A spot is only judged once it has been settled for <see cref="SettleSeconds"/>
        /// without a break, and each spot keeps its own clock, so walking up to one pin does not
        /// start the wait for another.
        /// </summary>
        internal static void Sweep()
        {
            float now = Time.time;
            if (now < _nextSweep) return;
            _nextSweep = now + SweepEvery;

            if (!Active())
            {
                SettledSince.Clear();
                return;
            }

            // A logout or a lost connection, before anything else is asked. Game.Shutdown saves
            // the map with every pin still on it and then ZNet.StopAll has ZDOMan.ShutDown empty
            // the sector lists and the portal list in one call. Everything else Sweep checks
            // survives to the end of that frame: the local player, the loaded zones, the map,
            // and ZDOMan.instance, which is never nulled and still equals _watched. A pass
            // landing in that frame would find every settled spot empty and forget it, and the
            // pin would come back from the map file next session with nothing to say it is
            // Varda's. m_shuttingDown is set before any of it is cleared.
            if (Game.instance == null || Game.instance.IsShuttingDown()
                || ZNet.GetConnectionStatus() != ZNet.ConnectionStatus.Connected)
            {
                SettledSince.Clear();
                return;
            }

            // Watch runs on spawn, straight after the sidecar for this world and character has
            // been read. Until it has, the list in memory can be the last world's, and a spot
            // from there checked against this world would be a pin taken off for nothing.
            ZDOMan zdoMan = ZDOMan.instance;
            if (zdoMan == null || zdoMan != _watched) return;

            Player player = Player.m_localPlayer;
            if (player == null || Minimap.instance == null || ZNet.instance == null
                || ZNetScene.instance == null || ZoneSystem.instance == null) return;

            // Mid-teleport the area around you is by definition still arriving, and afterwards
            // every clock should start again from the far end.
            if (player.IsTeleporting())
            {
                SettledSince.Clear();
                return;
            }

            Vector3 centre = ZNet.instance.GetReferencePosition();
            long me = player.GetPlayerID();

            SettledNow.Clear();

            foreach (Vector3 spot in Remembered.Of(Remembered.Portal))
            {
                if (!Settled(spot, centre)) continue;

                var key = new Vector2(spot.x, spot.z);
                SettledNow.Add(key);

                float since;
                if (!SettledSince.TryGetValue(key, out since))
                {
                    SettledSince[key] = now;
                    continue;
                }

                if (now - since < SettleSeconds) continue;

                Resolve(spot, me, null, FoundGone);
            }

            // A spot that is not settled right now loses its clock, so leaving before the wait
            // is up and coming back later starts it again rather than finding it already spent.
            NoLongerSettled.Clear();
            foreach (Vector2 key in SettledSince.Keys)
            {
                if (!SettledNow.Contains(key)) NoLongerSettled.Add(key);
            }

            foreach (Vector2 key in NoLongerSettled) SettledSince.Remove(key);
        }

        /// <summary>
        /// Whether this machine can be trusted to hold every ZDO on a spot, right now. The clock
        /// in Sweep is the third condition.
        ///
        /// <b>Why ZDOs, and not a look round the scene for a TeleportWorld.</b> On the machine
        /// hosting the world, singleplayer included, ZDOMan read every ZDO from the save before
        /// you spawned, so there a missing portal ZDO is simply a missing portal. A client has
        /// only what the server has sent, a package at a time, sorted by distance from where it
        /// last heard you were, and an empty spot means either gone or not here yet. Objects are
        /// worse on both counts: ZNetScene builds them from ten a frame upward (a hundredth of
        /// what is waiting, and more behind a loading screen), and only once the active area is
        /// loaded, so the set of objects trails the set of ZDOs and would be a second, slower
        /// stream to wait for on top of the first.
        ///
        /// <b>What settled has to mean.</b> First, the spot is inside the active area, which
        /// ZNetScene.InActiveArea measures from the reference position. That area is inside the
        /// ring of zones the server sends ordinary objects for; outside it you only ever get the
        /// distant ones, a portal is not one of them, and so a spot out there would read as empty
        /// however long you waited. The spot alone is enough, because the only portal that can
        /// keep this pin is the one standing on it. Second, ZNetScene.IsAreaReady, which is what
        /// the game's own loading screen waits on: the zone is loaded and every ZDO held around
        /// it has been built, so the client has at least caught up with what it was sent. What
        /// neither can say is whether anything is still on its way, and that is what the clock
        /// is for.
        ///
        /// If this is ever wrong anyway, it is usually wrong for a while rather than for good: a
        /// portal that still stands pins itself again from its own heartbeat as soon as it loads
        /// near you, because Tick finds no pin on it any more. Not when a pin you placed by hand
        /// sits within PortalMergeRadius of that portal. Tick finds that one, sees it is not
        /// Varda's, and leaves it alone, so the portal stays without a Varda pin for good. That
        /// is why the clock errs long, and why nothing here runs with Portals off, when Tick
        /// does not run at all.
        /// </summary>
        private static bool Settled(Vector3 spot, Vector3 centre)
        {
            return ZNetScene.InActiveArea(spot, centre) && ZNetScene.instance.IsAreaReady(spot);
        }

        /// <summary>
        /// The switches both moments answer to. Portals as well as RemoveDestroyedPortals, for
        /// the reason the class summary gives.
        /// </summary>
        private static bool Active()
        {
            return VardaConfig.Enabled.Value
                && VardaConfig.Portals.Value
                && VardaConfig.RemoveDestroyedPortals.Value;
        }
    }
}
