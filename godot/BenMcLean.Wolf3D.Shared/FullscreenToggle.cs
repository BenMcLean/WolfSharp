using Godot;

namespace BenMcLean.Wolf3D.Shared;

/// <summary>
/// Alt+Enter fullscreen toggle for flatscreen mode, shared by every scene/room
/// (action room, menu room, etc.) so behavior is consistent across the whole game.
/// </summary>
public static class FullscreenToggle
{
	public static bool IsToggleEvent(InputEventKey keyEvent) =>
		keyEvent.Keycode == Key.Enter && keyEvent.AltPressed;
	/// <summary>
	/// Toggles between windowed and exclusive fullscreen. Forces a 1920x1080 window size
	/// when entering fullscreen so screenshots/recordings are always 1080p.
	/// </summary>
	public static void Toggle()
	{
		if (DisplayServer.WindowGetMode() == DisplayServer.WindowMode.ExclusiveFullscreen)
			DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
		else
		{
			DisplayServer.WindowSetSize(new Vector2I(1920, 1080));
			DisplayServer.WindowSetMode(DisplayServer.WindowMode.ExclusiveFullscreen);
		}
	}
}
