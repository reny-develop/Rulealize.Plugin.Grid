// Copyright (c) 2026 Reny
// Licensed under the Apache License, Version 2.0.

using Rulealize.Abstraction.Building;
using Rulealize.Abstraction.Evaluation;
using Rulealize.Abstraction.Node;
using Rulealize.Abstraction.Value;

namespace Rulealize.Plugin.Grid
{
    /// <summary>The value of one square.</summary>
    /// <remarks>
    /// <para>
    /// Three different situations answer null: an empty square, a square off the board, and
    /// a null coordinate. That collapse is the most consequential decision in this plugin.
    /// </para>
    /// <para>
    /// Reversi's capture rule reads the square just past a run of opposing stones. When the
    /// run reaches the edge, the index is past the end of the ray, so the coordinate is null,
    /// so this answers null, so the equality test against the mover's colour answers false,
    /// so the run is not a capture. Correct, and nowhere in the rule set does anyone write a
    /// bounds check.
    /// </para>
    /// <para>
    /// The cost is that a mistyped literal coordinate reads as null instead of complaining.
    /// Coordinates are almost never literal — they come from <c>grid.coords</c> or
    /// <c>grid.ray</c> — so that was judged the cheaper of the two prices.
    /// </para>
    /// </remarks>
    internal sealed class AtNode(ExpressionNode board, ExpressionNode coordinate) : ExpressionNode
    {
        public static ExpressionNode Build(INodeBuildContext context) =>
            new AtNode(context.RequireExpression("grid"), context.RequireExpression("coord"));

        public override RuleValue Evaluate(IEvaluationContext context)
        {
            BoardValue value = GridArguments.RequireBoard(board.Evaluate(context), "grid.at.grid");
            CoordinateResult result = GridArguments.ReadCoordinate(
                coordinate.Evaluate(context),
                value.Geometry,
                "grid.at.coord",
                out int x,
                out int y);

            return result == CoordinateResult.OnBoard ? value[x, y] : RuleValue.Null;
        }
    }

    /// <summary>Every coordinate on the board.</summary>
    /// <remarks>
    /// <para>
    /// Row-major from the top left, and that order is fixed. <c>GetValidInputs</c> enumerates a
    /// parameter's domain to build its candidates, and its output should not depend on
    /// which run it was.
    /// </para>
    /// <para>
    /// This is where candidate generation starts. Reversi's placement takes one coordinate
    /// parameter whose domain is this sequence, giving sixty-four candidates for the guard
    /// to sift.
    /// </para>
    /// </remarks>
    internal sealed class CoordsNode(ExpressionNode board) : ExpressionNode
    {
        public static ExpressionNode Build(INodeBuildContext context) =>
            new CoordsNode(context.RequireExpression("of"));

        public override RuleValue Evaluate(IEvaluationContext context)
        {
            BoardValue value = GridArguments.RequireBoard(board.Evaluate(context), "grid.coords.of");
            return RuleValue.Sequence(() => Enumerate(value.Geometry));
        }

        private static IEnumerable<RuleValue> Enumerate(BoardGeometry geometry)
        {
            for (int y = 0; y < geometry.Height; y++)
            {
                for (int x = 0; x < geometry.Width; x++)
                {
                    yield return new CoordinateValue(geometry, x, y);
                }
            }
        }
    }

    /// <summary>Every cell value on the board, in the same order as <c>grid.coords</c>.</summary>
    /// <remarks>
    /// Expressible as <c>grid.coords</c> plus <c>grid.at</c>; shorter when only the values
    /// matter, as when counting how many stones of each colour are on the board.
    /// </remarks>
    internal sealed class CellsNode(ExpressionNode board) : ExpressionNode
    {
        public static ExpressionNode Build(INodeBuildContext context) =>
            new CellsNode(context.RequireExpression("of"));

        public override RuleValue Evaluate(IEvaluationContext context)
        {
            BoardValue value = GridArguments.RequireBoard(board.Evaluate(context), "grid.cells.of");
            return RuleValue.Sequence(() => Enumerate(value));
        }

        private static IEnumerable<RuleValue> Enumerate(BoardValue board)
        {
            for (int y = 0; y < board.Geometry.Height; y++)
            {
                for (int x = 0; x < board.Geometry.Width; x++)
                {
                    yield return board[x, y];
                }
            }
        }
    }
}
