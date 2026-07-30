namespace BenMcLean.Wolf3D.Assets;

/// <summary>
/// The seed plus the user-facing generation knobs for BenMcLean.Wolf3D.MapGenerator. This record
/// is the entire content of a "generated level" save — no map data is ever persisted for a
/// procedurally generated level; the same values always regenerate the same <c>GameMap</c>.
/// Lives in Assets (not MapGenerator) so both MapGenerator and Simulator/AssetManager can
/// reference it without a circular project reference (MapGenerator already depends on Assets).
/// </summary>
public sealed record GenerationParameters
{
	public ulong SeedA { get; init; }
	public ulong SeedB { get; init; }
	/// <summary>Obsidian's "ob_size" (PARAM.float_size_wolf_3d): overall level size/complexity.</summary>
	public int LevelSize { get; init; } = 36;
	/// <summary>PARAM.room_size_multiplier_wolf_3d: 1.0 matches Obsidian's own default cell_size of 12.</summary>
	public double RoomSizeMultiplier { get; init; } = 1.0;
	/// <summary>Which episode (1-based) to draw a level from.</summary>
	public int Episode { get; init; } = 1;
	/// <summary>
	/// Which map within the episode (1-based). Only used by LevelGenerator.Generate(); ignored by
	/// GenerateEpisode(). How many maps an episode has is NOT a constant across games — base Wolf3D
	/// always has 10 (map 9 is the boss map, map 10 is the Pacman bonus map), but Spear of Destiny's
	/// episode lengths are variable and data-driven (see LevelGenerator.GenerateEpisode's doc comment).
	/// </summary>
	public int Map { get; init; } = 1;
	/// <summary>GAME.FACTORY.toughness_factor override (monster.lua's toughness-budget battle simulation).</summary>
	public double ToughnessFactor { get; init; } = 0.40;
}
