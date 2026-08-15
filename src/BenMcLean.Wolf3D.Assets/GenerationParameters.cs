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
	/// <summary>
	/// Obsidian's "ob_size" (PARAM.float_size_wolf_3d): overall level size/complexity. Controls
	/// Level.plan_size (wolf/factory.lua: ob_size&lt;=22 -> 4, &lt;=48 -> 5, &lt;=58 -> 6, else -> 7) —
	/// how many block-cells the layout spans within the always-64x64 tile grid, NOT the grid size
	/// itself (that's fixed). Also gates gold-key placement: WOLF.decide_quests only adds a k_gold
	/// quest on a non-boss map when round(ob_size/25)==2, i.e. ob_size roughly 38-62 — at the
	/// previous default of 36 every generated map got round(36/25)==1, so gold keys structurally
	/// could never appear except via a boss kill (Q.give_key), and the sparser plan_size=5 layout
	/// that low a value produces also left less room for the boss quest itself to get placed.
	/// Confirmed by playtesting: 36 produced silver-only keys and a boss-less "normal elevator"
	/// exit on the boss map (map 9). 55 satisfies both (round(55/25)==2, plan_size=6).
	/// </summary>
	public int LevelSize { get; init; } = 55;
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
