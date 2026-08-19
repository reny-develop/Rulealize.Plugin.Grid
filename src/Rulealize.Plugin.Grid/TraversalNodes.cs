// Copyright (c) 2026 Reny
// Licensed under the Apache License, Version 2.0.

using System.Collections.Immutable;
using Rulealize.Abstraction;
using Rulealize.Abstraction.Building;
using Rulealize.Abstraction.Evaluation;
using Rulealize.Abstraction.Node;
using Rulealize.Abstraction.Value;

namespace Rulealize.Plugin.Grid
{
    /// <summary>The squares reached by stepping from <c>from</c> in direction <c>dir</c>.</summary>
    /// <remarks>
    /// <para>
    /// The starting square is <em>not</em> included; the sequence begins one step out and
    /// ends where the board does, or after <c>length</c> steps if one is given.
    /// </para>
    /// <para>
    /// Excluding the origin is what makes a ray short to use. Looking outward from a square
    /// is what rays are for, and a rule set that had to skip the first element every time
    /// would say so in every ray it wrote. A rule that wants the origin already has it.
    /// </para>
    /// <para>
    /// A start that is null or off the board gives the empty sequence rather than an error,
    /// so that rays out of a computed square need no guard.
    /// </para>
    /// <para>
    /// The result is walked more than once in practice — Reversi takes a prefix of a ray and
    /// then indexes into the same ray — so it is re-enumerable, as every sequence must be.
    /// </para>
    /// </remarks>
    internal sealed class RayNode(
        ExpressionNode board,
        ExpressionNode origin,
        ExpressionNode direction,
        ExpressionNode? length) : ExpressionNode
    {
        public static ExpressionNode Build(INodeBuildContext context) =>
            new RayNode(
                context.RequireExpression("grid"),
                context.RequireExpression("from"),
                context.RequireExpression("dir"),
                context.OptionalExpression("length"));

        public override RuleValue Evaluate(IEvaluationContext context)
        {
            BoardValue value = GridArguments.RequireBoard(board.Evaluate(context), "grid.ray.grid");
            CoordinateResult start = GridArguments.ReadCoordinate(
                origin.Evaluate(context),
                value.Geometry,
                "grid.ray.from",
                out int x,
                out int y);

            if (start != CoordinateResult.OnBoard)
            {
                return RuleValue.EmptySequence;
            }

            DirectionValue step = GridArguments.RequireDirection(direction.Evaluate(context), "grid.ray.dir");

            int limit = int.MaxValue;
            if (length is not null)
            {
                limit = length.Evaluate(context).AsInt32("grid.ray.length");
                if (limit < 0)
                {
                    throw new RuleEvaluationException("grid.ray.length", $"A length cannot be negative, but is {limit}.");
                }
            }

            return RuleValue.Sequence(() => Walk(value.Geometry, x, y, step, limit));
        }

        private static IEnumerable<RuleValue> Walk(BoardGeometry geometry, int x, int y, DirectionValue step, int limit)
        {
            for (int taken = 0; taken < limit; taken++)
            {
                x += step.DeltaX;
                y += step.DeltaY;

                if (!geometry.Contains(x, y))
                {
                    yield break;
                }

                yield return new CoordinateValue(geometry, x, y);
            }
        }
    }

    /// <summary>A fixed set of directions.</summary>
    /// <remarks>
    /// <para>
    /// The order is fixed — row-major, starting from the top left neighbour — for the same
    /// reason coordinates are: results should not depend on the run.
    /// </para>
    /// <para>
    /// The board is taken as an argument so that the directions returned agree with its
    /// axes. There is no way to name one direction on its own yet; a rule that needs
    /// "forwards" for a piece cannot express it. Reversi does not care, because it treats
    /// all eight alike.
    /// </para>
    /// </remarks>
    internal sealed class DirectionsNode(ExpressionNode board, ImmutableArray<RuleValue> directions) : ExpressionNode
    {
        private static readonly ImmutableArray<RuleValue> Orthogonal =
        [
            new DirectionValue(0, -1),
            new DirectionValue(-1, 0),
            new DirectionValue(1, 0),
            new DirectionValue(0, 1)
        ];

        private static readonly ImmutableArray<RuleValue> Diagonal =
        [
            new DirectionValue(-1, -1),
            new DirectionValue(1, -1),
            new DirectionValue(-1, 1),
            new DirectionValue(1, 1)
        ];

        private static readonly ImmutableArray<RuleValue> Eight =
        [
            new DirectionValue(-1, -1),
            new DirectionValue(0, -1),
            new DirectionValue(1, -1),
            new DirectionValue(-1, 0),
            new DirectionValue(1, 0),
            new DirectionValue(-1, 1),
            new DirectionValue(0, 1),
            new DirectionValue(1, 1)
        ];

        public static ExpressionNode Build(INodeBuildContext context)
        {
            string kind = context.RequireString("kind");
            ImmutableArray<RuleValue> directions = kind switch
            {
                "orthogonal" => Orthogonal,
                "diagonal" => Diagonal,
                "eight" => Eight,
                _ => throw context.Error("kind", $"'{kind}' is not one of orthogonal, diagonal, eight.")
            };

            return new DirectionsNode(context.RequireExpression("of"), directions);
        }

        public override RuleValue Evaluate(IEvaluationContext context)
        {
            _ = GridArguments.RequireBoard(board.Evaluate(context), "grid.directions.of");
            return RuleValue.Sequence(directions);
        }
    }
}
