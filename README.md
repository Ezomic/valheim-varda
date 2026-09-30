# Varda

Dungeons pin themselves on your map when you go in. Portals you build pin themselves with
the tag you gave them.

## Why

A *varða* is a cairn: a stack of stones somebody left beside a route so the next person
could find it again. It marks where people have been. It does not point at anything nobody
has found yet, and that distinction is the entire design of this mod.

Every map mod arrives, sooner or later, at pinning ore. It is the most requested feature and
it is the one that quietly ends the game it is installed in: a detector tells you where
something is that you have not discovered, and the part of Valheim that was exploring
becomes the part that is reading a list. Silver is the clearest case. Finding it is a
mountain, a wishbone, a night out in the cold and a real chance of dying, and all of that
collapses the moment the map knows.

What Varda pins is memory, not discovery. You walked into that crypt at dusk and came out
somewhere else entirely. You built that portal and tagged it *mine*. Neither pin tells you a
single thing you did not already know. It saves you opening the map, holding the mouse
still and typing a name, thirty times an evening, while something is chewing on you.

Which is also why the trigger is going *in* and not walking past. A pin for every cave mouth
within sight of the road is a detector with extra steps, and "I have been down there" and
"there is a door over there" are different facts about the world.

## What it actually does

Two things.

**A dungeon pins itself the moment you step through the door.** Burial chambers, troll
caves, sunken crypts, frost caves, mines, anything the game moves you into. It carries the
dungeon's own name, the one on the banner you see as you go in, so a modded dungeon is
labelled correctly without this mod knowing anything about it. Coming back out does not add
a second pin, and neither does going back in next week.

**A portal you built pins itself with its tag,** and the pin follows the tag when you change
it. Every portal gets a pin of its own, so a hub of portals built side by side shows each one
under its own tag. Portals somebody else built are never pinned: that is their network, and
putting it on your map is reading their notes rather than writing your own.

**When the portal goes, its pin goes with it.** Take it down with the hammer, lose it to a
troll, or have somebody else break it, and the pin comes off your map. Walking away from a
portal is not the same thing and never counts. If it went while you were not there, say while
you were offline or on the other side of the world, the pin can stay until you are next
standing near that spot. Once the area around you has finished loading and there is still no
portal of yours there, the pin comes off then. Only the pin Varda put on that portal is taken.
Break one portal in a row and the pins on the others stay, as does a pin you placed beside it
by hand and any dungeon pin. `RemoveDestroyedPortals`
in the settings turns this off, if you would rather keep a mark where a portal used to be.

**One portal can be kept off your map.** Look at a portal you built and press H: its pin comes
off and stays off. Press H again while looking at it and the pin comes back, with its tag. The
portal's hover text says which the key will do, under the line for setting its tag. It is
yours alone, remembered per character, per world and per portal. Nobody else's map changes, the
portal itself is untouched, and a pin you placed by hand is never touched. While
`RemoveDestroyedPortals` is on, a destroyed portal is forgotten along with the fact that you hid
it, at once if you are there and otherwise the next time you stand near that spot, so a new
portal built in its place gets a pin. With it off, the choice stays with the spot, and a new
portal built exactly there starts hidden. The key is `HidePortalKey` in the settings. Setting it
to None takes away the key and its line on the portal, but a portal you had already hidden stays
off your map until you bind a key again and press it on that portal. It is not Shift+E because
another of my mods already uses that on portals.

## These are ordinary pins

Worth knowing before you install, because it is what happens when you uninstall.

A Varda pin is a real map pin. You can rename it, tick it off, filter it with the buttons
along the top of the map, and delete it. It is written into your map save with all the
others, and it is the same kind of pin you would have placed by hand. The mod only paints
its own picture on top of it.

That is deliberate. Valheim saves a pin as a name, a position and its *type as a plain
number*, and a number it does not recognise comes back as the plain marker. A mod that
invented its own pin type would leave all of its pins looking the same the first time the game
loads without it, crypts and portals alike, and filed under the plain marker's filter button.
Varda saves a portal as the game's own portal pin, so uninstalling it costs you the pictures it
paints and nothing else.

The one thing that is stored outside the map is which pins are Varda's, so their icons can
be put back after a reload, and so a destroyed portal takes only its own pin with it. The
portals you hid are written down there too. It is a small text file per character per world, in
`BepInEx/config/Varda/`. Deleting it loses the pictures and no pins, though pins made before
then stay put when their portal goes, and a portal you hid gets its pin back.

## A pin already there is left alone

If there is any pin within a few metres of a dungeon door, Varda adds nothing, whoever put it
there. So a spot you have already marked by hand stays exactly as you marked it, with your
name on it, and re-entering does not stack a second pin on top of the first. That distance is
`MergeRadius`, 8 metres.

Portals keep the same rule over a much shorter distance, `PortalMergeRadius`, one metre.
Portals are built side by side a few metres apart, and at 8 metres one pin you had placed in
the middle of a row would leave every portal around it without a pin of its own. So a portal
only goes without one when another pin sits right on top of it, and the pins Varda put on
your other portals never count. A metre is also what the game itself treats as the same spot
for a pin, when it merges a map from a cartography table. If you pinned your portals by hand
before installing Varda and your pins sit a little off, you get Varda's beside yours. Delete
your own, since Varda's follows the tag, or raise the setting.

## Icons

Dungeon and portal pins can each wear a PNG dropped beside `Varda.dll`. If there is no file,
dungeon pins borrow the icon the game already uses for a crypt, and failing that both fall
back to the plain pin. None of that is an error and none of it stops the mod working. An
icon is the part you can change without a rebuild, which is the point of it being a file.

## Installing

Needs BepInEx. Nothing else. Through a mod manager it is one install. By hand, put
`Varda.dll` in `BepInEx/plugins/Varda/`.

Then start the game once and quit. That first run writes the config file. It does not exist
before the mod has loaded, which is the usual reason people think it is broken.

## Settings

The file is `BepInEx/config/ezomic.valheim.varda.cfg`. Open it in any text editor. Every
setting has a comment above it, so the file explains itself.

Note that changing a default in a new version does nothing on a machine that has already run
the mod. BepInEx writes every entry on first run and the saved value wins.

## Multiplayer

**Nobody else needs it.** Purely local and purely visual: it reads the world you are already
being sent and writes to your own map file. A server does not know it is there, and neither
does anybody playing beside you.

If [Core](https://github.com/Ezomic/valheim-core) is installed, this mod registers with its
version gate so a mismatch is reported rather than discovered later. Every setting here is
marked as yours: a host running Varda does not get to decide what is on your personal map.

## Bugs and ideas

Both go to the site. [longhouse.thijssensoftware.nl/bugs](https://longhouse.thijssensoftware.nl/bugs)
is for anything broken, and [longhouse.thijssensoftware.nl/ideas](https://longhouse.thijssensoftware.nl/ideas)
is for what a mod should do next. You can vote on other people's ideas there as well.

Signing in takes a Steam or Discord account. I work from that list, so the votes decide what
I pick up next.

## Licence

MIT. See `LICENSE`.
