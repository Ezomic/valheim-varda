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
it. Portals somebody else built are never pinned: that is their network, and putting it on
your map is reading their notes rather than writing your own.

**When the portal goes, its pin goes with it.** Take it down with the hammer, lose it to a
troll, or have somebody else break it, and the pin comes off your map. Walking away from a
portal is not the same thing and never counts. If it went at a time your game had never
loaded it, say while you were offline or before you had been near it that session, nothing
told your game, so the pin stays until you are next standing near that spot. Once the area
around you has finished loading and there is still no portal of yours there, the pin comes
off then. Only the pin Varda put on that portal is taken: a pin you
placed beside it by hand stays, and so does any dungeon pin. `RemoveDestroyedPortals` in the
settings turns this off, if you would rather keep a mark where a portal used to be.

## These are ordinary pins

Worth knowing before you install, because it is what happens when you uninstall.

A Varda pin is a real map pin. You can rename it, tick it off, filter it with the buttons
along the top of the map, and delete it. It is written into your map save with all the
others, and it is the same kind of pin you would have placed by hand. The mod only paints
its own picture on top of it.

That is deliberate, and the alternative is worse than it sounds. Valheim saves a pin as a
name, a position and its *type as a plain number*, and it reads that number back through a
function that rejects anything it does not recognise. A mod that invented its own pin type
would produce pins that vanish from the map the first time the game loads without the mod,
silently, and then the next save writes the map back without them. Uninstalling Varda costs
you an icon. It does not cost you the pin.

The one thing that is stored outside the map is which pins are Varda's, so their icons can
be put back after a reload, and so a destroyed portal takes only its own pin with it. It is a
small text file per character per world, in `BepInEx/config/Varda/`. Deleting it loses the
pictures and no pins, though pins made before then stay put when their portal goes.

## A pin already there is left alone

If there is any pin within a few metres of the door, Varda adds nothing, whoever put it
there. So a spot you have already marked by hand stays exactly as you marked it, with your
name on it, and re-entering does not stack a second pin on top of the first.

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
