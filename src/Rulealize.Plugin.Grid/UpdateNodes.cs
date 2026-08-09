// Copyright (c) 2026 Reny
// Licensed under the Apache License, Version 2.0.

using Rulealize.Abstraction.Building;
using Rulealize.Abstraction.Evaluation;
using Rulealize.Abstraction.Node;
using Rulealize.Abstraction.Value;

namespace Rulealize.Plugin.Grid
{
    /// <summary>A board with one square replaced, as a value.</summary>
    /// <remarks>
    /// <para>
    /// The expression counterpart of <c>grid.set</c>. It writes nothing: it takes a board and
    /// returns another, and the one it was given is unchanged.
    /// </para>
    /// <para>
    /// This exists because a guard cannot see the position a move would produce. An input's
    /// <c>when</c> is evaluated against the state as it is, and effects run only once the
    /// guard has passed — which is the right way round for Othello, whose legality is a
    /// property of the position in front of it, but not for chess, where a move is illegal
    /// precisely when the position after it leaves its own king attacked. A rule set that can
    /// build the resulting board as a value can ask that question of it.
    /// </para>
    /// <para>
    /// It is worth being clear about how far this goes. It answers the question for a board,
    /// because a board is one value in one field. A rule whose legality depends on the whole
    /// state after the move — several fields at once — still cannot be written, and would
    /// need something from the runtime rather than from here.
    /// </para>
    /// <para>
    /// Strict about its coordinate, like <c>grid.set</c> and unlike <c>grid.at</c>: a write
    /// off the board or to a null coordinate has no meaning except to discard itself.
    /// </para>
    /// </remarks>
    internal sealed class WithNode(ExpressionNode board, ExpressionNode coordinate, ExpressionNode value)
        : ExpressionNode
    {
        public static ExpressionNode Build(INodeBuildContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            return new WithNode(
                context.RequireExpression("grid"),
                context.RequireExpression("coord"),
                context.RequireExpression("value"));
        }

        public override RuleValue Evaluate(IEvaluationContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            BoardValue current = GridArguments.RequireBoard(board.Evaluate(context), "grid.with.grid");
            GridArguments.ReadWritableCoordinate(
                coordinate.Evaluate(context),
                current.Geometry,
                "grid.with.coord",
                out int x,
                out int y);

            return current.With(x, y, value.Evaluate(context));
        }
    }

    /// <summary>A board with several squares replaced by one value, as a value.</summary>
    /// <remarks>
    /// The expression counterpart of <c>grid.setMany</c>. The value is evaluated once and
    /// written to every coordinate. An empty sequence returns the board unchanged, which is
    /// what lets a rule set apply a move's incidental removals — the pawn an en passant
    /// capture takes, the rook a castle moves — without first asking whether there are any.
    /// </remarks>
    internal sealed class WithManyNode(ExpressionNode board, ExpressionNode coordinates, ExpressionNode value)
        : ExpressionNode
    {
        public static ExpressionNode Build(INodeBuildContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            return new WithManyNode(
                context.RequireExpression("grid"),
                context.RequireExpression("coords"),
                context.RequireExpression("value"));
        }

        public override RuleValue Evaluate(IEvaluationContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            BoardValue current = GridArguments.RequireBoard(board.Evaluate(context), "grid.withMany.grid");
            SequenceValue targets = coordinates.Evaluate(context).AsSequence("grid.withMany.coords");

            List<(int X, int Y)> positions = [];
            foreach (RuleValue target in targets)
            {
                context.CancellationToken.ThrowIfCancellationRequested();

                GridArguments.ReadWritableCoordinate(
                    target,
                    current.Geometry,
                    "grid.withMany.coords",
                    out int x,
                    out int y);

                positions.Add((x, y));
            }

            return positions.Count == 0 ? current : current.With(positions, value.Evaluate(context));
        }
    }
}
