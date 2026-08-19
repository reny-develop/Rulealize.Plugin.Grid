// Copyright (c) 2026 Reny
// Licensed under the Apache License, Version 2.0.

using Rulealize.Abstraction.Building;
using Rulealize.Abstraction.Evaluation;
using Rulealize.Abstraction.Node;
using Rulealize.Abstraction.Value;

namespace Rulealize.Plugin.Grid
{
    /// <summary>Writes one square.</summary>
    /// <remarks>
    /// <para>
    /// An effect node, so it can only appear in an input's <c>effects</c> array.
    /// </para>
    /// <para>
    /// The board is read from the draft rather than from the snapshot, so that two effects
    /// writing to the same board add up. The coordinate and the value are evaluated against
    /// the snapshot, so what they compute is unaffected by whatever the earlier effect did.
    /// Reversi relies on both halves at once: placing a stone and flipping the captured ones
    /// are separate effects on one board, and the set of captured stones is worked out from
    /// the position as it was before the stone went down.
    /// </para>
    /// <para>
    /// A coordinate that is null or off the board is an error here, where <c>grid.at</c>
    /// would have answered null. Reads are forgiving because "there is nothing there" is a
    /// real answer; writes are strict because the only thing an out-of-range write could do
    /// is discard itself, and a rule quietly writing nowhere is a rule that is wrong.
    /// </para>
    /// </remarks>
    internal sealed class SetNode(StatePath board, ExpressionNode coordinate, ExpressionNode value) : EffectNode
    {
        public static EffectNode Build(INodeBuildContext context) =>
            new SetNode(
                TargetBoard.Resolve(context),
                context.RequireExpression("coord"),
                context.RequireExpression("value"));

        public override void Apply(IEvaluationContext context, IStateDraft draft)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(draft);

            BoardValue current = GridArguments.RequireBoard(draft.Get(board), "grid.set.target");
            GridArguments.ReadWritableCoordinate(
                coordinate.Evaluate(context),
                current.Geometry,
                "grid.set.coord",
                out int x,
                out int y);

            draft.Set(board, current.With(x, y, value.Evaluate(context)));
        }
    }

    /// <summary>Writes one value to several squares.</summary>
    /// <remarks>
    /// <para>
    /// An effect node. The value is evaluated once and written to every coordinate; it is
    /// not re-evaluated per square, so there is no way to write different values with one
    /// node.
    /// </para>
    /// <para>
    /// An empty sequence of coordinates does nothing. Reversi's flip effect gets one
    /// whenever a move captures in no direction — which, given that the guard has already
    /// established the move is legal, does not happen, but the rule set does not have to
    /// know that.
    /// </para>
    /// </remarks>
    internal sealed class SetManyNode(StatePath board, ExpressionNode coordinates, ExpressionNode value) : EffectNode
    {
        public static EffectNode Build(INodeBuildContext context) =>
            new SetManyNode(
                TargetBoard.Resolve(context),
                context.RequireExpression("coords"),
                context.RequireExpression("value"));

        public override void Apply(IEvaluationContext context, IStateDraft draft)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(draft);

            BoardValue current = GridArguments.RequireBoard(draft.Get(board), "grid.setMany.target");
            SequenceValue targets = coordinates.Evaluate(context).AsSequence("grid.setMany.coords");

            List<(int X, int Y)> positions = [];
            foreach (RuleValue target in targets)
            {
                context.CancellationToken.ThrowIfCancellationRequested();

                GridArguments.ReadWritableCoordinate(
                    target,
                    current.Geometry,
                    "grid.setMany.coords",
                    out int x,
                    out int y);

                positions.Add((x, y));
            }

            if (positions.Count == 0)
            {
                return;
            }

            draft.Set(board, current.With(positions, value.Evaluate(context)));
        }
    }

    /// <summary>Resolves the <c>target</c> of a board-writing effect.</summary>
    /// <remarks>
    /// The target is written <c>"$board"</c>, which builds into a node belonging to the
    /// state plugin — an assembly this one does not reference and cannot inspect. What it
    /// can do is ask for <see cref="IStateLocation"/>, the contract both plugins already
    /// share through the abstraction, and take the resolved path from it. Checking the
    /// field's schema at the same time turns "that is not a board" from an evaluation
    /// failure into a build error.
    /// </remarks>
    internal static class TargetBoard
    {
        public static StatePath Resolve(INodeBuildContext context)
        {
            ExpressionNode target = context.RequireExpression("target");
            if (target is not IStateLocation location)
            {
                throw context.Error("target", "must denote a state field, such as \"$board\".");
            }

            if (location.Path.Schema is not BoardSchemaNode)
            {
                throw context.Error("target", $"'{location.Path}' is not a board.");
            }

            return location.Path;
        }
    }
}
