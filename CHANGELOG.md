# Changelog

## 0.1.0 - unreleased

First version. Builds and deploys; **never run in game**.

Dungeons pin themselves when you go in, portals you built pin themselves with their tag.
Both are ordinary vanilla pins wearing a borrowed or supplied picture, which is what makes
uninstalling cost an icon rather than the pin — a custom pin type would be dropped on load
by anyone without the mod, silently, and written out of the map on the next save.

Two things are unverified and both need one session to settle:

- **Which sprite a dungeon pin ends up wearing.** The icon the game uses for a crypt is
  serialised on the Minimap prefab inside a bundle, so it is readable from the running game
  and nowhere else — not from the assemblies and not from the asset manifest. Turning on
  `Verbose` writes both of the map's sprite tables to the log at the first spawn, which is
  also what the `DungeonPinType` and `PortalPinType` defaults were guessed at without.
- **Whether the borrow finds anything at all.** The donor names came from Delve, which has
  itself never been run, so "vanilla carries a crypt entry" is an assumption inherited
  rather than a fact checked. If it finds nothing, pins wear the plain icon and a PNG beside
  the DLL fixes it without a rebuild.

Hand-placed extra icons are designed and not started; they wait on the icon set being drawn.
