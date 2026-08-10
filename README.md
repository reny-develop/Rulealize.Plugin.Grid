# Rulealize.Plugin.Grid

Two-dimensional boards, coordinates, directions and ray traversal for
[Rulealize](https://github.com/reny-develop/Rulealize) rule sets.

| | |
| --- | --- |
| Plugin id | `Rulealize.Plugin.Grid` |
| Namespace | `grid` |
| Reserved prefix | none |
| Depends on | `Rulealize.Abstraction` |
| Specification | [doc/specification.md](doc/specification.md) |

`grid.board` and `grid.square` are schema nodes; `grid.at`, `coords`, `cells`, `ray`,
`directions`, `with` and `withMany` are expressions; `grid.set` and `grid.setMany` are
effects. The only plugin in the standard set that provides all three kinds of node.

**There is no Othello in here.** Capturing and flipping are not concepts this plugin has;
a rule set assembles them out of a ray and a take-while. Whether that boundary holds is the
test of whether the plugin decomposition works at all, and the same vocabulary should
describe five-in-a-row, draughts, or a cellular automaton. Chess and shogi would need
another plugin for captured pieces and promotion, not a bigger grid.

Two decisions to know before reading the specification. **Reads are forgiving and writes
are strict**: `grid.at` answers null three different ways — an empty square, a square off
the board, a null coordinate — and that collapse is what keeps Reversi's capture rule free
of bounds checks, while `grid.set` refuses both null and off-board coordinates because a
rule quietly writing nowhere is a rule that is wrong. And a board's JSON form is a **sparse
object**, decided here rather than by the state plugin, which moves the value in and out of
a field without looking inside it.

## How the effects write

`grid.set` is handed `"$board"` as its target, which builds into a node owned by the state
plugin — an assembly this one does not reference. What makes the write possible is
`IStateLocation`, the contract in `Rulealize.Abstraction` that both plugins already depend
on. Asking the target for it yields a resolved state path, and the schema at that path is
checked at the same time, so pointing `grid.set` at a counter is a build error rather than
a surprise.

## Building

`Rulealize.Abstraction` is not on nuget.org yet, so `NuGet.config` points at a folder
feed. Produce it from the abstraction repository first:

```
dotnet pack path\to\Rulealize.Abstraction\src\Rulealize.Abstraction -c Release -o path\to\LocalNuGet
```

with `LocalNuGet` a sibling of this repository. Then `dotnet build`.

## License

Apache-2.0.
