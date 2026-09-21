# Changelog

## 0.1.0 - unreleased

First version. Builds and deploys; **never played**, though it has now been run far enough to
read the map's own art out of it.

Dungeons pin themselves when you go in, portals you built pin themselves with their tag. Both
are ordinary vanilla pins wearing a supplied picture, which is what makes uninstalling cost an
icon rather than the pin — a custom pin type would be dropped on load by anyone without the
mod, silently, and written out of the map on the next save.

The dungeon pin has its own art, drawn by `tools/icons_build.py` and loaded from beside the
DLL. It is a stone arch with steps going down into the dark, from a reference Robbin supplied
after fourteen shapes that did not work. Every one of those drew an opening and became a
horseshoe, because punching a hole to the bottom edge splits the mass into two legs. The steps
are what fix it: they sit in the bottom of the opening and join the jambs across it.

Settled by running it, and none of it was readable any other way:

- **The five hand-placed pin types are Icon0 fire, Icon1 house, Icon2 hammer, Icon3 plain
  marker, Icon4 portal.** Serialised on the Minimap prefab inside a bundle, so in neither the
  assemblies nor the asset manifest. `DumpIcons` writes the whole UI atlas out and logs each
  sprite's rect, which is how they were read.
- **The dungeon default moved from Icon2 to Icon3.** The hammer is what players already use to
  mark a mine, so a dungeon pin on Icon2 would look like somebody's own mining pin and hide
  with it whenever that filter row was switched off.
- **`PortalIcon` is empty by default.** Icon4 already wears the game's own portal icon, so
  shipping art for it would be replacing correct vanilla art with a copy of it.
- **There is no crypt sprite to borrow.** `m_locationIcons` carries five entries — start
  temple, trader, Hildir's camp, Bog Witch camp, ancient upgrade station — and none is a
  dungeon. The donor list inherited from Delve matches nothing.

Still unverified: everything about the pins in play. No dungeon has been entered and no portal
built with the mod loaded, so the two features it exists for have been read and not watched.
