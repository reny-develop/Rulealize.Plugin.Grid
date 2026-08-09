// Copyright (c) 2026 Reny
// Licensed under the Apache License, Version 2.0.

using System.Text.Json;
using Rulealize.Abstraction.Building;
using Rulealize.Abstraction.Node;
using Rulealize.Abstraction.Value;

namespace Rulealize.Plugin.Grid
{
    /// <summary>The schema of a board field.</summary>
    /// <remarks>
    /// <para>
    /// A schema node, so it appears in <c>state.schema</c> and nowhere else, and is never
    /// evaluated.
    /// </para>
    /// <para>
    /// It also owns the board's JSON form, which is a sparse object keyed by coordinate with
    /// empty squares left out entirely:
    /// </para>
    /// <code>
    /// "board": { "d4": "white", "e4": "black", "d5": "black", "e5": "white" }
    /// </code>
    /// <para>
    /// That is shorter than writing out sixty-four squares and produces readable diffs, but
    /// the reason it can be decided here at all is the point: the state plugin moves this
    /// value in and out of a field without knowing what is inside it, so switching to a
    /// dense array would change this file and nothing else. In memory the board is dense
    /// already; sparseness is a serialization choice.
    /// </para>
    /// <para>
    /// The cell type is any schema node. This plugin does not reference the one that
    /// usually supplies it.
    /// </para>
    /// </remarks>
    internal sealed class BoardSchemaNode(BoardGeometry geometry, SchemaNode cell) : SchemaNode
    {
        /// <summary>Gets the board's shape.</summary>
        public BoardGeometry Geometry => geometry;

        /// <inheritdoc />
        public override bool IsNullable => false;

        public static SchemaNode Build(INodeBuildContext context) =>
            new BoardSchemaNode(DeclaredGeometry.Read(context), context.RequireSchema("cell"));

        /// <inheritdoc />
        public override void Validate(RuleValue value, ISchemaValidationSink sink)
        {
            ArgumentNullException.ThrowIfNull(value);
            ArgumentNullException.ThrowIfNull(sink);

            if (value is not BoardValue board)
            {
                sink.Violation($"Expected a {geometry} board but got {RuleValue.Describe(value)}.");
                return;
            }

            if (!board.Geometry.Equals(geometry))
            {
                sink.Violation($"Expected a {geometry} board but got a {board.Geometry} one.");
                return;
            }

            for (int y = 0; y < geometry.Height; y++)
            {
                for (int x = 0; x < geometry.Width; x++)
                {
                    cell.Validate(board[x, y], new SquareValidationSink(sink, geometry.Format(x, y)));
                }
            }
        }

        /// <inheritdoc />
        public override RuleValue ReadJson(JsonElement element, ISchemaValidationSink sink)
        {
            ArgumentNullException.ThrowIfNull(sink);

            BoardValue board = BoardValue.Empty(geometry);
            if (element.ValueKind != JsonValueKind.Object)
            {
                sink.Violation("Expected an object mapping coordinates to cell values.");
                return board;
            }

            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (!geometry.TryParse(property.Name, out int x, out int y))
                {
                    sink.Violation(property.Name, $"is not a coordinate in {geometry.NotationName} notation.");
                    continue;
                }

                if (!geometry.Contains(x, y))
                {
                    sink.Violation(property.Name, $"is not a square of a {geometry} board.");
                    continue;
                }

                RuleValue value = cell.ReadJson(property.Value, new SquareValidationSink(sink, property.Name));
                board = board.With(x, y, value);
            }

            // Cells the document left out stay null. When the cell type does not allow that,
            // the omission is a violation, and reporting it here rather than at read time is
            // what makes a hand-written board with a missing square diagnosable.
            if (!cell.IsNullable)
            {
                ReportMissingSquares(board, sink);
            }

            return board;
        }

        /// <inheritdoc />
        public override void WriteJson(Utf8JsonWriter writer, RuleValue value)
        {
            ArgumentNullException.ThrowIfNull(writer);

            BoardValue board = GridArguments.RequireBoard(value, "grid.board");

            writer.WriteStartObject();
            for (int y = 0; y < geometry.Height; y++)
            {
                for (int x = 0; x < geometry.Width; x++)
                {
                    RuleValue square = board[x, y];
                    if (square.IsNull)
                    {
                        continue;
                    }

                    writer.WritePropertyName(geometry.Format(x, y));
                    cell.WriteJson(writer, square);
                }
            }

            writer.WriteEndObject();
        }

        private void ReportMissingSquares(BoardValue board, ISchemaValidationSink sink)
        {
            for (int y = 0; y < geometry.Height; y++)
            {
                for (int x = 0; x < geometry.Width; x++)
                {
                    if (board[x, y].IsNull)
                    {
                        sink.Violation(geometry.Format(x, y), "is missing, and this board's cells cannot be null.");
                    }
                }
            }
        }
    }

    /// <summary>Reports a cell's violations under the coordinate they were found at.</summary>
    /// <remarks>
    /// The cell schema knows nothing about boards and reports its violations unqualified.
    /// This puts them where they happened, so that a malformed state document names the
    /// square rather than the field.
    /// </remarks>
    internal sealed class SquareValidationSink(ISchemaValidationSink inner, string square) : ISchemaValidationSink
    {
        public bool HasViolations => inner.HasViolations;

        public void Violation(string message) => inner.Violation(square, message);

        public void Violation(string relativePath, string message) =>
            inner.Violation($"{square}/{relativePath}", message);
    }
}
