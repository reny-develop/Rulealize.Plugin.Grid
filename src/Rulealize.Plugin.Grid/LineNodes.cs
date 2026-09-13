// Copyright (c) 2026 Reny
// Licensed under the Apache License, Version 2.0.

using Rulealize.Abstraction.Building;
using Rulealize.Abstraction.Evaluation;
using Rulealize.Abstraction.Node;
using Rulealize.Abstraction.Value;

namespace Rulealize.Plugin.Grid
{
    /// <summary>How long a line of one value through a square is.</summary>
    /// <remarks>
    /// <para>
    /// The rule this exists for is "n of these in a row", and the specification said for two
    /// versions that it was expected to come out of <c>grid.ray</c> plus
    /// <see href="https://github.com/reny-develop/Rulealize.Plugin.Sequence">Sequence</see>'s
    /// <c>seq.takeWhile</c>. It does, and that was the wrong place to leave it: what comes
    /// out is two definitions and one clause per axis, because a run reaches both ways from a
    /// square and <b>nothing pairs a direction with its opposite</b>. A three in a row is
    /// forty-three lines of document, and it is the same forty-three lines every time.
    /// </para>
    /// <para>
    /// <b>The square itself is not read.</b> It counts as one and the walk starts one step
    /// out, both ways, exactly as <c>grid.ray</c> excludes its origin. That is not an
    /// accident of the implementation: the question a guard asks is what a move <i>would</i>
    /// make, and the piece is not on the board when the guard asks — effects read the
    /// snapshot, so it is not there afterwards either.
    /// </para>
    /// <para>
    /// The direction and its opposite give the same answer, which is why
    /// <c>grid.directions</c> grew a <c>kind</c> that returns one direction per line.
    /// </para>
    /// </remarks>
    internal sealed class RunNode(
        ExpressionNode board,
        ExpressionNode origin,
        ExpressionNode axis,
        ExpressionNode value) : ExpressionNode
    {
        public static ExpressionNode Build(INodeBuildContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            return new RunNode(
                context.RequireExpression("grid"),
                context.RequireExpression("from"),
                context.RequireExpression("axis"),
                context.RequireExpression("value"));
        }

        public override RuleValue Evaluate(IEvaluationContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            BoardValue grid = GridArguments.RequireBoard(board.Evaluate(context), "grid.run.grid");
            CoordinateResult start = GridArguments.ReadCoordinate(
                origin.Evaluate(context),
                grid.Geometry,
                "grid.run.from",
                out int x,
                out int y);

            if (start != CoordinateResult.OnBoard)
            {
                return RuleValue.Number(0);
            }

            DirectionValue step = GridArguments.RequireDirection(axis.Evaluate(context), "grid.run.axis");
            RuleValue wanted = value.Evaluate(context);

            return RuleValue.Number(
                1
                + Reach(context, grid, x, y, step.DeltaX, step.DeltaY, wanted)
                + Reach(context, grid, x, y, -step.DeltaX, -step.DeltaY, wanted));
        }

        private static int Reach(
            IEvaluationContext context,
            BoardValue grid,
            int x,
            int y,
            int dx,
            int dy,
            RuleValue wanted)
        {
            int reached = 0;

            while (true)
            {
                context.CancellationToken.ThrowIfCancellationRequested();

                x += dx;
                y += dy;

                if (!grid.Geometry.Contains(x, y) || !grid[x, y].Equals(wanted))
                {
                    return reached;
                }

                reached++;
            }
        }
    }
}
