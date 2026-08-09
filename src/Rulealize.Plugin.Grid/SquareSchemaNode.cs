// Copyright (c) 2026 Reny
// Licensed under the Apache License, Version 2.0.

using System.Text.Json;
using Rulealize.Abstraction.Building;
using Rulealize.Abstraction.Node;
using Rulealize.Abstraction.Value;

namespace Rulealize.Plugin.Grid
{
    /// <summary>Reads the <c>width</c>, <c>height</c> and <c>coord</c> a schema node declares.</summary>
    /// <remarks>
    /// Shared by the two schema nodes here, which describe different things about the same
    /// shape of board and would otherwise disagree about it by drifting apart.
    /// </remarks>
    internal static class DeclaredGeometry
    {
        public static BoardGeometry Read(INodeBuildContext context)
        {
            int width = context.RequireInt32("width");
            int height = context.RequireInt32("height");

            if (width < 1)
            {
                throw context.Error("width", $"must be at least 1, but is {width}.");
            }

            if (height < 1)
            {
                throw context.Error("height", $"must be at least 1, but is {height}.");
            }

            string notationText = context.OptionalString("coord") ?? "index";
            CoordinateNotation notation = notationText switch
            {
                "index" => CoordinateNotation.Index,
                "algebraic" => CoordinateNotation.Algebraic,
                _ => throw context.Error("coord", $"'{notationText}' is not one of index, algebraic.")
            };

            if (notation == CoordinateNotation.Algebraic && width > 26)
            {
                throw context.Error(
                    "coord",
                    $"algebraic notation has one letter per column, so it cannot describe {width} of them.");
            }

            return new BoardGeometry(width, height, notation);
        }
    }

    /// <summary>The schema of a field holding one square of a board.</summary>
    /// <remarks>
    /// <para>
    /// A schema node, appearing in <c>state.schema</c> and nowhere else. It is written like a
    /// board without the cells:
    /// </para>
    /// <code>
    /// "passed": { "op": "grid.square", "width": 8, "height": 8, "coord": "algebraic", "nullable": true }
    /// </code>
    /// <para>
    /// Without it, the state has nowhere to keep a coordinate. The scalar types describe text
    /// and numbers, and a coordinate stored as text stops being a coordinate: it can still be
    /// handed to <c>grid.at</c>, which reads both forms, but it can no longer be compared with
    /// one that came from <c>grid.coords</c>, because text and an opaque coordinate are
    /// different kinds and the value model holds different kinds to be unequal. Chess needs
    /// exactly one such field — the square a pawn skipped over, which the next move may
    /// capture on — and until this existed the only way to keep it was a second board with one
    /// square marked.
    /// </para>
    /// <para>
    /// The JSON form is the board's notation, so the field reads <c>"e3"</c> rather than a pair
    /// of numbers, and <see langword="null"/> when nullable and absent.
    /// </para>
    /// </remarks>
    internal sealed class SquareSchemaNode(BoardGeometry geometry, bool nullable) : SchemaNode
    {
        /// <summary>Gets the shape of the board this square belongs to.</summary>
        public BoardGeometry Geometry => geometry;

        /// <inheritdoc />
        public override bool IsNullable => nullable;

        public static SchemaNode Build(INodeBuildContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            return new SquareSchemaNode(
                DeclaredGeometry.Read(context),
                context.OptionalBoolean("nullable", false));
        }

        /// <inheritdoc />
        public override void Validate(RuleValue value, ISchemaValidationSink sink)
        {
            ArgumentNullException.ThrowIfNull(value);
            ArgumentNullException.ThrowIfNull(sink);

            if (value.IsNull)
            {
                if (!nullable)
                {
                    sink.Violation("Expected a square but got null.");
                }

                return;
            }

            if (value is not CoordinateValue coordinate)
            {
                sink.Violation($"Expected a square but got {RuleValue.Describe(value)}.");
                return;
            }

            if (!geometry.Contains(coordinate.X, coordinate.Y))
            {
                sink.Violation($"{coordinate} is not a square of a {geometry} board.");
            }
        }

        /// <inheritdoc />
        public override RuleValue ReadJson(JsonElement element, ISchemaValidationSink sink)
        {
            ArgumentNullException.ThrowIfNull(sink);

            if (element.ValueKind == JsonValueKind.Null)
            {
                if (!nullable)
                {
                    sink.Violation("Expected a square but got null.");
                }

                return RuleValue.Null;
            }

            if (element.ValueKind != JsonValueKind.String)
            {
                sink.Violation($"Expected a square in {geometry.NotationName} notation.");
                return RuleValue.Null;
            }

            string text = element.GetString()!;
            if (!geometry.TryParse(text, out int x, out int y))
            {
                sink.Violation($"\"{text}\" is not a coordinate in {geometry.NotationName} notation.");
                return RuleValue.Null;
            }

            if (!geometry.Contains(x, y))
            {
                sink.Violation($"\"{text}\" is not a square of a {geometry} board.");
                return RuleValue.Null;
            }

            return new CoordinateValue(geometry, x, y);
        }

        /// <inheritdoc />
        public override void WriteJson(Utf8JsonWriter writer, RuleValue value)
        {
            ArgumentNullException.ThrowIfNull(writer);
            ArgumentNullException.ThrowIfNull(value);

            if (value.IsNull)
            {
                writer.WriteNullValue();
                return;
            }

            writer.WriteStringValue(value.GetCanonicalText());
        }
    }
}
