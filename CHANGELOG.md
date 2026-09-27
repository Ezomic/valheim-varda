# Changelog

## 0.1.0 - unreleased

First version. Builds and deploys, **never played**, though it has been run far enough to read
the map's own art out of it.

Dungeons pin themselves when you go in, portals you built pin themselves with their tag. Both
are ordinary vanilla pins wearing a supplied picture, so uninstalling costs you an icon rather
than the pin. A custom pin type would come back as the plain marker for anyone without the mod,
every one of them alike.

A destroyed portal takes its pin with it: at once if you are there, otherwise by the next time
you are. Not run in game yet.

Every portal gets a pin of its own, so a row of tagged portals side by side shows every tag. Not
run in game yet.

The dungeon pin has its own art, drawn by `tools/icons_build.py` and loaded from beside the
DLL. It is a stone arch with steps going down into the dark, from a reference Robbin supplied
after fourteen shapes that did not work. Every one of those came out a horseshoe, because
punching an opening to the bottom edge splits the mass into two legs. The steps sit in the
bottom of the opening and join the jambs across it.

Settled by running it, none of it readable any other way:

- The five hand-placed pin types are Icon0 fire, Icon1 house, Icon2 hammer, Icon3 plain marker,
  Icon4 portal. They are serialised on the Minimap prefab inside a bundle, so they are in
  neither the assemblies nor the asset manifest. `DumpIcons` writes the whole UI atlas out and
  logs each sprite's rect, which is how they were read.
- The dungeon default moved from Icon2 to Icon3. The hammer is what players already use to mark
  a mine, so a dungeon on Icon2 would look like somebody's own mining pin and hide with it
  whenever that filter row was switched off.
- `PortalIcon` is empty by default. Icon4 already wears the game's own portal icon.
- There is no crypt sprite to borrow. `m_locationIcons` carries five entries, being the start
  temple, trader, Hildir's camp, Bog Witch camp and ancient upgrade station. The donor list
  inherited from Delve matches none of them.

Both halves were run in game on 2026-09-22, by `varda-pins-a-dungeon` and
`varda-pins-your-own-portal`. That covered the pin appearing on the way in and not for walking
past, the label being suppressed, a second entry not stacking a pin, a portal pinning itself
once, and two portals 24 m apart getting a pin each. The portal scenario now puts the second
portal five metres from the first, to check portals side by side, and that version has not run
yet.

**`NameDungeons` is off.** The icon says what the thing is. Turn it on to tell two crypts apart
without opening them.
