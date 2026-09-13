// Copyright (c) 2026 Reny
// Licensed under the Apache License, Version 2.0.

using Rulealize.Abstraction.Plugin;

namespace Rulealize.Plugin.Grid
{
    /// <summary>
    /// Two-dimensional boards, coordinates, directions and ray traversal, over the
    /// <c>grid</c> namespace.
    /// </summary>
    /// <remarks>
    /// <para>
    /// There is no Reversi here. Capturing and flipping are not concepts this plugin has;
    /// a rule set builds them out of a ray and a take-while. Whether that boundary holds is
    /// the test of whether the plugin decomposition works at all, and it does: the same
    /// vocabulary should describe five-in-a-row, draughts, or a cellular automaton.
    /// </para>
    /// <para>
    /// The only plugin in the standard set that provides all three kinds of node — a schema
    /// for the board, expressions to read it, effects to write it.
    /// </para>
    /// </remarks>
    public sealed class GridPlugin : IRulealizePlugin
    {
        /// <inheritdoc />
        public PluginManifest Manifest { get; } =
            new("Rulealize.Plugin.Grid", new Version(1, 2, 0), "grid");

        /// <inheritdoc />
        public void Register(IPluginRegistry registry)
        {
            ArgumentNullException.ThrowIfNull(registry);

            registry.AddSchema("board", BoardSchemaNode.Build);
            registry.AddSchema("square", SquareSchemaNode.Build);
            registry.AddExpression("at", AtNode.Build);
            registry.AddExpression("coords", CoordsNode.Build);
            registry.AddExpression("cells", CellsNode.Build);
            registry.AddExpression("ray", RayNode.Build);
            registry.AddExpression("directions", DirectionsNode.Build);
            registry.AddExpression("run", RunNode.Build);
            registry.AddExpression("with", WithNode.Build);
            registry.AddExpression("withMany", WithManyNode.Build);
            registry.AddEffect("set", SetNode.Build);
            registry.AddEffect("setMany", SetManyNode.Build);
        }
    }
}
