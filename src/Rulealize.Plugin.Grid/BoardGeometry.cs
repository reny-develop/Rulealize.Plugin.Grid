// Copyright (c) 2026 Reny
// Licensed under the Apache License, Version 2.0.

using System.Globalization;

namespace Rulealize.Plugin.Grid
{
    /// <summary>How a board's coordinates are written down.</summary>
    internal enum CoordinateNotation
    {
        /// <summary><c>"&lt;x&gt;,&lt;y&gt;"</c>, zero-based, origin top left, y downwards.</summary>
        Index,

        /// <summary>A column letter and a one-based row, origin bottom left, y upwards.</summary>
        Algebraic
    }

    /// <summary>A board's dimensions and coordinate notation.</summary>
    /// <remarks>
    /// <para>
    /// Positions are held internally as zero-based <c>(x, y)</c> with the origin at the top
    /// left and y increasing downwards, whatever notation the board was declared with.
    /// Algebraic notation flips the vertical axis on the way in and out: <c>a1</c> is the
    /// bottom left square, as it is on a chessboard.
    /// </para>
    /// <para>
    /// The two notations disagreeing about which way is up is a genuine trap, and it was
    /// chosen anyway. Each is conventional in its own setting, and following the convention
    /// produces fewer misreadings than imposing one orientation on both would.
    /// </para>
    /// </remarks>
    internal sealed class BoardGeometry(int width, int height, CoordinateNotation notation) : IEquatable<BoardGeometry>
    {
        /// <summary>Gets the number of columns.</summary>
        public int Width => width;

        /// <summary>Gets the number of rows.</summary>
        public int Height => height;

        /// <summary>Gets the notation coordinates are written in.</summary>
        public CoordinateNotation Notation => notation;

        /// <summary>Gets the number of squares.</summary>
        public int CellCount => width * height;

        /// <summary>Gets the notation's name, for error messages.</summary>
        public string NotationName => notation == CoordinateNotation.Algebraic ? "algebraic" : "index";

        /// <summary>Determines whether a position lies on the board.</summary>
        /// <param name="x">The column.</param>
        /// <param name="y">The row.</param>
        /// <returns><see langword="true"/> when the position is on the board.</returns>
        public bool Contains(int x, int y) => x >= 0 && x < width && y >= 0 && y < height;

        /// <summary>Writes a position in this board's notation.</summary>
        /// <param name="x">The column.</param>
        /// <param name="y">The row.</param>
        /// <returns>The text form, such as <c>"d3"</c>.</returns>
        public string Format(int x, int y) => notation == CoordinateNotation.Algebraic
            ? string.Concat(
                ((char)('a' + x)).ToString(),
                (height - y).ToString(CultureInfo.InvariantCulture))
            : string.Concat(
                x.ToString(CultureInfo.InvariantCulture),
                ",",
                y.ToString(CultureInfo.InvariantCulture));

        /// <summary>Reads a position written in this board's notation.</summary>
        /// <param name="text">The text form.</param>
        /// <param name="x">Receives the column.</param>
        /// <param name="y">Receives the row.</param>
        /// <returns><see langword="true"/> when the text is well formed.</returns>
        /// <remarks>
        /// Well formed is not the same as on the board. Text that parses but points off the
        /// edge succeeds here, and each node decides for itself what to do about that —
        /// reads answer null, writes fault.
        /// </remarks>
        public bool TryParse(string text, out int x, out int y)
        {
            x = 0;
            y = 0;

            if (notation == CoordinateNotation.Algebraic)
            {
                if (text.Length < 2 || !char.IsAsciiLetterLower(text[0]))
                {
                    return false;
                }

                if (!int.TryParse(text.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out int row)
                    || row < 1)
                {
                    return false;
                }

                x = text[0] - 'a';
                y = height - row;
                return true;
            }

            int comma = text.IndexOf(',');
            if (comma <= 0)
            {
                return false;
            }

            return int.TryParse(text.AsSpan(0, comma), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out x)
                && int.TryParse(text.AsSpan(comma + 1), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out y);
        }

        /// <inheritdoc />
        public bool Equals(BoardGeometry? other) =>
            other is not null && other.Width == width && other.Height == height && other.Notation == notation;

        /// <inheritdoc />
        public override bool Equals(object? obj) => Equals(obj as BoardGeometry);

        /// <inheritdoc />
        public override int GetHashCode() => HashCode.Combine(width, height, notation);

        /// <inheritdoc />
        public override string ToString() =>
            $"{width.ToString(CultureInfo.InvariantCulture)}x{height.ToString(CultureInfo.InvariantCulture)} ({NotationName})";
    }
}
