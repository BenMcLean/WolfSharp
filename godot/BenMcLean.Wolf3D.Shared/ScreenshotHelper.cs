using System;
using Godot;

namespace BenMcLean.Wolf3D.Shared;

/// <summary>
/// P-key screenshot capture, shared by every scene/room so behavior is consistent
/// across the whole game. Saves PNGs under user://screenshots/.
/// </summary>
public static class ScreenshotHelper
{
	private const string ScreenshotDirectory = "user://screenshots";
	public static bool IsCaptureEvent(InputEventKey keyEvent) =>
		keyEvent.Keycode == Key.P;
	public static void Capture(Viewport viewport)
	{
		Image image = viewport.GetTexture()?.GetImage();
		if (image is null)
		{
			GD.PrintErr("ERROR: ScreenshotHelper.Capture: viewport texture returned no image.");
			return;
		}
		DirAccess.MakeDirRecursiveAbsolute(ScreenshotDirectory);
		string path = $"{ScreenshotDirectory}/screenshot_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png";
		Error error = image.SavePng(path);
		if (error != Error.Ok)
			GD.PrintErr($"ERROR: ScreenshotHelper.Capture: failed to save '{path}' ({error}).");
	}
}
