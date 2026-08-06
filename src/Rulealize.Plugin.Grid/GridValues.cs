// Copyright (c) 2026 Reny
// Licensed under the Apache License, Version 2.0.

using System.Globalization;
using Rulealize.Abstraction.Values;

namespace Rulealize.Plugin.Grid
{
    /// <summary>A position on a board.</summary>
    /// <remarks>
    /// <para>
    /// Its canonical text is what <c>GetValidInputs</c> writes into an input's arguments and
    /// what an incoming input document is read back from. Without that round trip a
    /// coordinate could not be an input parameter at all.
    /// </para>
    /// <para>
    /// Two coordinates are equal when their positions match. The board they came from is
    /// carried so that the text form can be produced, not as part of identity.
    /// </para>
    /// </remarks>
    internal sealed class CoordinateValue(BoardGeometry geometry, int x, int y) : OpaqueValue
    {
        /// <summary>Gets the board's shape, which decides how this reads as text.</summary>
        public BoardGeometry Geometry => geometry;

        /// <summary>Gets the column.</summary>
        public int X => x;

        /// <summary>Gets the row.</summary>
        public int Y => y;

        /// <inheritdoc />
        public override string TypeTag => "grid/coord";

        /// <inheritdoc />
        public override string? GetCanonicalText() => geometry.Format(x, y);

        /// <inheritdoc />
        public override int GetHashCode() => HashCode.Combine("grid/coord", x, y);

        /// <inheritdoc />
        public override string ToString() => geometry.Format(x, y);

        /// <inheritdoc />
        protected override bool EqualsCore(OpaqueValue other) =>
            other is CoordinateValue coordinate && coordinate.X == x && coordinate.Y == y;
    }

    /// <summary>A step from one square to a neighbouring one.</summary>
    /// <remarks>Written as <c>"&lt;dx&gt;,&lt;dy&gt;"</c> in the board's internal axes.</remarks>
    internal sealed class DirectionValue(int deltaX, int deltaY) : OpaqueValue
    {
        /// <summary>Gets the horizontal step.</summary>
        public int DeltaX => deltaX;

        /// <summary>Gets the vertical step.</summary>
        public int DeltaY => deltaY;

        /// <inheritdoc />
        public override string TypeTag => "grid/direction";

        /// <inheritdoc />
        public override string? GetCanonicalText() =>
            string.Concat(
                deltaX.ToString(CultureInfo.InvariantCulture),
                ",",
                deltaY.ToString(CultureInfo.InvariantCulture));

        /// <inheritdoc />
        public override int GetHashCode() => HashCode.Combine("grid/direction", deltaX, deltaY);

        /// <inheritdoc />
        public override string ToString() => GetCanonicalText()!;

        /// <inheritdoc />
        protected override bool EqualsCore(OpaqueValue other) =>
            other is DirectionValue direction && direction.DeltaX == deltaX && direction.DeltaY == deltaY;
    }

    /// <summary>A board: a rectangle of cell values.</summary>
    /// <remarks>
    /// <para>
    /// Immutable. Writing a square produces a new board, which is what lets the effects of
    /// one input accumulate in a draft without anything being mutated underneath an
    /// expression that is still reading the snapshot.
    /// </para>
    /// <para>
    /// Held densely and serialized sparsely. Nothing outside this plugin depends on either
    /// choice: the schema node owns the JSON form, and the state plugin moves the value
    /// around without looking inside.
    /// </para>
    /// <para>
    /// A board has no canonical text and does not need one — it never appears as an input
    /// argument, only inside the state, where its schema node serializes it.
    /// </para>
    /// </remarks>
    internal sealed class BoardValue : OpaqueValue
    {
        private readonly RuleValue[] _cells;

        private BoardValue(BoardGeometry geometry, RuleValue[] cells)
        {
            Geometry = geometry;
            _cells = cells;
        }

        /// <summary>Gets the board's shape.</summary>
        public BoardGeometry Geometry { get; }

        /// <inheritdoc />
        public override string TypeTag => "grid/board";

        /// <summary>Gets the value of a square that is known to be on the board.</summary>
        /// <param name="x">The column.</param>
        /// <param name="y">The row.</param>
        /// <returns>The cell value, null for an empty square.</returns>
        public RuleValue this[int x, int y] => _cells[(y * Geometry.Width) + x];

        /// <summary>Creates a board with every square empty.</summary>
        /// <param name="geometry">The board's shape.</param>
        /// <returns>The board.</returns>
        public static BoardValue Empty(BoardGeometry geometry)
        {
            RuleValue[] cells = new RuleValue[geometry.CellCount];
            Array.Fill(cells, RuleValue.Null);
            return new BoardValue(geometry, cells);
        }

        /// <summary>Returns a board with one square replaced.</summary>
        /// <param name="x">The column.</param>
        /// <param name="y">The row.</param>
        /// <param name="value">The new cell value.</param>
        /// <returns>A new board. This one is unchanged.</returns>
        public BoardValue With(int x, int y, RuleValue value)
        {
            RuleValue[] cells = (RuleValue[])_cells.Clone();
            cells[(y * Geometry.Width) + x] = value;
            return new BoardValue(Geometry, cells);
        }

        /// <summary>Returns a board with several squares replaced by one value.</summary>
        /// <param name="positions">The squares, each known to be on the board.</param>
        /// <param name="value">The new cell value.</param>
        /// <returns>A new board. This one is unchanged.</returns>
        public BoardValue With(IEnumerable<(int X, int Y)> positions, RuleValue value)
        {
            RuleValue[] cells = (RuleValue[])_cells.Clone();
            foreach ((int x, int y) in positions)
            {
                cells[(y * Geometry.Width) + x] = value;
            }

            return new BoardValue(Geometry, cells);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            HashCode hash = default;
            hash.Add(Geometry);
            foreach (RuleValue cell in _cells)
            {
                hash.Add(cell);
            }

            return hash.ToHashCode();
        }

        /// <inheritdoc />
        protected override bool EqualsCore(OpaqueValue other)
        {
            if (other is not BoardValue board || !board.Geometry.Equals(Geometry))
            {
                return false;
            }

            for (int i = 0; i < _cells.Length; i++)
            {
                if (!_cells[i].Equals(board._cells[i]))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
