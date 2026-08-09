# Rulealize.Plugin.Grid

Two-dimensional boards, coordinates, directions and ray traversal for
[Rulealize](https://github.com/reny-develop/Rulealize) rule sets.

| | |
| --- | --- |
| Plugin id | `Rulealize.Plugin.Grid` |
| Namespace | `grid` |
| Reserved prefix | none |
| Depends on | `Rulealize.Abstraction` |

**There is no Othello in here.** Capturing and flipping are not concepts this plugin has;
a rule set assembles them out of a ray and a take-while. Whether that boundary holds is the
test of whether the plugin decomposition works at all, and the same vocabulary should
describe five-in-a-row, draughts, or a cellular automaton. Chess and shogi would need
another plugin for captured pieces and promotion, not a bigger grid.

The only plugin in the standard set that provides all three kinds of node.

## Operations

| Operation | Kind | Shape |
| --- | --- | --- |
| `grid.board` | schema | `{ "op": "grid.board", "width": …, "height": …, "coord": "…", "cell": <schema> }` |
| `grid.square` | schema | `{ "op": "grid.square", "width": …, "height": …, "coord": "…", "nullable": … }` |
| `grid.at` | expression | `{ "op": "grid.at", "grid": …, "coord": … }` |
| `grid.coords` | expression | `{ "op": "grid.coords", "of": … }` |
| `grid.cells` | expression | `{ "op": "grid.cells", "of": … }` |
| `grid.ray` | expression | `{ "op": "grid.ray", "grid": …, "from": …, "dir": …, "length": … }` |
| `grid.directions` | expression | `{ "op": "grid.directions", "of": …, "kind": "eight" }` |
| `grid.with` | expression | `{ "op": "grid.with", "grid": …, "coord": …, "value": … }` |
| `grid.withMany` | expression | `{ "op": "grid.withMany", "grid": …, "coords": …, "value": … }` |
| `grid.set` | effect | `{ "op": "grid.set", "target": …, "coord": …, "value": … }` |
| `grid.setMany` | effect | `{ "op": "grid.setMany", "target": …, "coords": …, "value": … }` |

`grid.with` and `grid.withMany` write nothing. They take a board and return another, leaving
the one they were given alone, and they exist because a guard cannot see the position a move
would produce: `when` is evaluated against the state as it is, and effects run only after it
passes. That is the right way round for Othello, whose legality is a property of the position
in front of it, and the wrong way round for chess, where a move is illegal exactly when the
position after it leaves its own king attacked. A rule set that can build the resulting board
as a value can ask the question of it.

They answer it for a board, which is one value in one field. A rule whose legality depends on
the whole state after a move is still out of reach and would need something from the runtime
rather than from here.

`grid.square` describes a field holding a single square, written in the board's own notation:

```jsonc
"passed": { "op": "grid.square", "width": 8, "height": 8, "coord": "algebraic", "nullable": true }
```

Without it the state has nowhere to keep a coordinate. Kept as text it can still be handed to
`grid.at`, which reads both forms, but it can no longer be compared with one that came out of
`grid.coords`, because text and an opaque coordinate are different kinds and the value model
holds different kinds to be unequal. Chess needs exactly one such field — the square a pawn
skipped over, which the next move may capture on.

Two opaque types come with them: `grid/coord`, written the way the board's notation says,
and `grid/direction`, written `"<dx>,<dy>"`.

## Coordinates

`coord` on the board picks the notation.

| Notation | Form | 8×8, top left / bottom right |
| --- | --- | --- |
| `"algebraic"` | column letter, one-based row | `a8` / `h1` |
| `"index"` | `"<x>,<y>"`, zero-based | `0,0` / `7,7` |

Algebraic puts the origin at the bottom left with y upwards, as chess and Othello do.
Index puts it at the top left with y downwards. The two disagreeing about which way is up
is a genuine trap, and it was chosen anyway: each is conventional in its own setting, and
following the convention misleads fewer readers than imposing one orientation on both.

Algebraic needs one letter per column, so it is only available for boards up to 26 wide.

Every node that takes a coordinate accepts either the opaque value — as produced by
`grid.coords` or `grid.ray` — or its text form, read in the board's notation. That is what
lets a parameter domain enumerate opaque coordinates while an input document still says
`{ "at": "d3" }`.

Text that does not parse is an evaluation error. Text that parses but points off the board
is not; what happens then is each node's business, and reads and writes differ.

## Reads are forgiving, writes are strict

`grid.at` answers null three different ways: an empty square, a square off the board, and
a null coordinate.

That collapse is the most consequential decision here. Othello's capture rule reads the
square just past a run of opposing stones:

```jsonc
"coord": { "op": "seq.elementAt", "source": "@ray",
           "index": { "op": "seq.count", "source": "@run" } }
```

When the run reaches the edge of the board, the index is past the end of the ray, so the
coordinate is null, so `grid.at` is null, so the equality test against the mover's colour
is false, so the run is not a capture. All correct, and nowhere does the rule set write a
bounds check. **Off the board, off the end, and empty all collapsing into one answer is
what keeps the Othello rule set short.**

The price is that a mistyped literal coordinate reads as null instead of complaining.
Coordinates are almost never literal — they come from `grid.coords` or `grid.ray` — so the
price was judged worth paying. A strict `grid.atStrict` remains an option.

`grid.set` and `grid.setMany` refuse both null and off-board coordinates. A read off the
board has a real answer; a write off the board has no meaningful behaviour except to
discard itself, and a rule quietly writing nowhere is a rule that is wrong.

## Rays

A ray starts one step out and ends where the board does, or after `length` steps.

**The origin is not included.** Looking outward from a square is what rays are for, and
including the origin would mean skipping the first element in every ray anyone wrote. A
rule that wants the origin already has it.

A start that is null or off the board gives the empty sequence rather than an error.

The result gets walked more than once — Othello takes a prefix of a ray and then indexes
into the same ray — so it is re-enumerable, as the value model requires of every sequence.

## Effects and the draft

`grid.set` reads its board from the **draft**, not the snapshot, so that two effects
writing to one board add up. Their coordinates and values are evaluated against the
snapshot, so what they compute is unaffected by the earlier write.

Othello uses both halves at once:

```jsonc
[
  { "op": "grid.set", "target": "$board", "coord": "@at", "value": "#me" },
  { "op": "grid.setMany", "target": "$board",
    "coords": { "op": "def.call", "def": "flips", "args": { "at": "@at" } },
    "value": "#me" }
]
```

The second effect starts from a board that already has the new stone on it, but works out
which stones were captured from the position as it stood before the move.

`target` is written `"$board"`, which builds into a node owned by the state plugin — an
assembly this one does not reference. What makes the write possible is `IStateLocation`,
the contract in `Rulealize.Abstraction` that both plugins already depend on. Asking the
target for it yields a resolved state path, and the schema at that path is checked at the
same time, so pointing `grid.set` at a counter is a build error rather than a surprise.

## The board's JSON form

A sparse object, empty squares left out:

```jsonc
"board": { "d4": "white", "e4": "black", "d5": "black", "e5": "white" }
```

Shorter than sixty-four squares and it diffs readably, but the reason it can be decided
here at all is the point. The state plugin moves the value in and out of a field without
looking inside it, so switching to a dense array would change one file. In memory the board
is dense already; sparseness is only how it is written down.

The cell type is any schema node — `{ "op": "type.enum", "values": ["black", "white"],
"nullable": true }` in Othello's case. This plugin does not reference the one that supplies
it; `cell` is simply "some schema".

## Not provided

- **Neighbours.** `grid.ray` with `"length": 1`.
- **A single named direction.** Only whole sets so far. Chess and shogi will need one.
- **Turn-relative directions.** "Forwards" depends on whose turn it is, and a grid has no
  business knowing that.
- **Per-square values in one effect.** `grid.setMany` writes one value everywhere.
- **Non-rectangular boards.** Hex and holes belong in a different plugin.
- **Ordering on coordinates.** Blocked on the value model growing a notion of an orderable
  opaque value.
- **A filtered `grid.coords`.** Othello's sixty-four candidates are fine; a larger board or
  a multi-parameter input will want the domain narrowed before the guard runs.

## Building

`Rulealize.Abstraction` is not on nuget.org yet, so `NuGet.config` points at a folder
feed. Produce it from the abstraction repository first:

```
dotnet pack path\to\Rulealize.Abstraction\src\Rulealize.Abstraction -c Release -o path\to\LocalNuGet
```

with `LocalNuGet` a sibling of this repository. Then `dotnet build`.

## License

Apache-2.0.
