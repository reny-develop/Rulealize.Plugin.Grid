// Copyright (c) 2026 Reny
// Licensed under the Apache License, Version 2.0.

using Rulealize.Abstraction;
using Rulealize.Abstraction.Values;

namespace Rulealize.Plugin.Grid
{
    /// <summary>What came back when a node asked for a coordinate.</summary>
    internal enum CoordinateResult
    {
        /// <summary>A position on the board.</summary>
        OnBoard,

        /// <summary>The value was null; there is no position.</summary>
        Absent,

        /// <summary>A well-formed position, but not one this board has.</summary>
        OffBoard
    }

    /// <summary>Turns the values a node was handed into boards, coordinates and directions.</summary>
    /// <remarks>
    /// <para>
    /// A coordinate arrives in either of two forms, and both are accepted. Opaque, when it
    /// came from <c>grid.coords</c> or <c>grid.ray</c>; text, when it came from an input
    /// document's arguments and was written in the board's notation. That double acceptance
    /// is what allows a parameter domain to enumerate opaque coordinates while an input is
    /// still written <c>{ "at": "d3" }</c>.
    /// </para>
    /// <para>
    /// Text that does not parse is an evaluation error — the rule set said something that
    /// is not a coordinate at all. Text that parses but points off the board is not:
    /// callers decide, and they decide differently.
    /// </para>
    /// </remarks>
    internal static class GridArguments
    {
        /// <summary>Requires a value to be a board.</summary>
        /// <param name="value">The value.</param>
        /// <param name="origin">Where it came from, for the error message.</param>
        /// <returns>The board.</returns>
        public static BoardValue RequireBoard(RuleValue value, string origin)
        {
            if (value is BoardValue board)
            {
                return board;
            }

            throw new RuleEvaluationException(origin, $"Expected a board but got {RuleValue.Describe(value)}.");
        }

        /// <summary>Requires a value to be a direction.</summary>
        /// <param name="value">The value, either a direction or its text form.</param>
        /// <param name="origin">Where it came from, for the error message.</param>
        /// <returns>The direction.</returns>
        public static DirectionValue RequireDirection(RuleValue value, string origin)
        {
            if (value is DirectionValue direction)
            {
                return direction;
            }

            if (value is TextValue text)
            {
                int comma = text.Value.IndexOf(',');
                if (comma > 0
                    && int.TryParse(text.Value.AsSpan(0, comma), out int deltaX)
                    && int.TryParse(text.Value.AsSpan(comma + 1), out int deltaY))
                {
                    return new DirectionValue(deltaX, deltaY);
                }

                throw new RuleEvaluationException(
                    origin,
                    $"\"{text.Value}\" is not a direction; a direction is written \"<dx>,<dy>\".");
            }

            throw new RuleEvaluationException(origin, $"Expected a direction but got {RuleValue.Describe(value)}.");
        }

        /// <summary>Reads a coordinate argument against a board.</summary>
        /// <param name="value">The value, either a coordinate, its text form, or null.</param>
        /// <param name="geometry">The board it is to be read against.</param>
        /// <param name="origin">Where it came from, for the error message.</param>
        /// <param name="x">Receives the column, when there is one.</param>
        /// <param name="y">Receives the row, when there is one.</param>
        /// <returns>Whether the value named a square, named none, or named one off the board.</returns>
        public static CoordinateResult ReadCoordinate(
            RuleValue value,
            BoardGeometry geometry,
            string origin,
            out int x,
            out int y)
        {
            x = 0;
            y = 0;

            if (value.IsNull)
            {
                return CoordinateResult.Absent;
            }

            if (value is CoordinateValue coordinate)
            {
                x = coordinate.X;
                y = coordinate.Y;
            }
            else if (value is TextValue text)
            {
                if (!geometry.TryParse(text.Value, out x, out y))
                {
                    throw new RuleEvaluationException(
                        origin,
                        $"\"{text.Value}\" is not a coordinate in {geometry.NotationName} notation.");
                }
            }
            else
            {
                throw new RuleEvaluationException(origin, $"Expected a coordinate but got {RuleValue.Describe(value)}.");
            }

            return geometry.Contains(x, y) ? CoordinateResult.OnBoard : CoordinateResult.OffBoard;
        }

        /// <summary>Reads a coordinate that a write is about to be made to.</summary>
        /// <param name="value">The value.</param>
        /// <param name="geometry">The board it is to be read against.</param>
        /// <param name="origin">Where it came from, for the error message.</param>
        /// <param name="x">Receives the column.</param>
        /// <param name="y">Receives the row.</param>
        /// <remarks>
        /// Writes are strict where reads are forgiving. A read off the board has a
        /// meaningful answer — there is nothing there — but a write off the board has no
        /// meaningful behaviour except to discard itself, and a rule that is quietly writing
        /// nowhere is a rule that is wrong.
        /// </remarks>
        public static void ReadWritableCoordinate(
            RuleValue value,
            BoardGeometry geometry,
            string origin,
            out int x,
            out int y)
        {
            switch (ReadCoordinate(value, geometry, origin, out x, out y))
            {
                case CoordinateResult.OnBoard:
                    return;

                case CoordinateResult.Absent:
                    throw new RuleEvaluationException(origin, "Cannot write to a null coordinate.");

                default:
                    throw new RuleEvaluationException(
                        origin,
                        $"Cannot write to {geometry.Format(x, y)}; it is not on a {geometry} board.");
            }
        }
    }
}
