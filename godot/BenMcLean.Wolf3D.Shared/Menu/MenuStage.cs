using BenMcLean.Wolf3D.Assets.Gameplay;
using BenMcLean.Wolf3D.Assets.Menu;
using Godot;

namespace BenMcLean.Wolf3D.Shared.Menu;

/// <summary>
/// Godot scene root for the menu system.
/// Hosts MenuManager and displays the menu viewport.
/// Similar to ActionStage but for menus instead of gameplay.
/// </summary>
public partial class MenuStage : Node
{
	private MenuManager _menuManager;
	private ColorRect _marginBackground;
	private TextureRect _menuTextureRect;
	private Vector2I _lastWindowSize;
	/// <summary>
	/// Gets the menu manager instance.
	/// </summary>
	public MenuManager MenuManager => _menuManager;
	/// <summary>
	/// Gets the current menu state.
	/// Used by Root.cs to detect StartGame flag.
	/// </summary>
	public MenuState SessionState => _menuManager?.SessionState;
	/// <summary>
	/// Called when the node is added to the scene tree.
	/// Initializes the menu system.
	/// </summary>
	public override void _Ready()
	{
		// Get menu data from SharedAssetManager
		if (SharedAssetManager.CurrentGame?.MenuCollection is not MenuCollection menuCollection)
		{
			GD.PrintErr("ERROR: No MenuCollection in SharedAssetManager.CurrentGame");
			return;
		}
		// Get config from SharedAssetManager (or create default)
		// Initialize SharedAssetManager.Config if not already set
		SharedAssetManager.Config ??= new Config();
		// Create MenuManager (menus don't need RNG/GameClock - not deterministic)
		_menuManager = new MenuManager(
			menuCollection,
			SharedAssetManager.Config,
			SharedAssetManager.MenuLuaEngine,
			scriptsPrecompiled: SharedAssetManager.MenuLuaEngine is not null);
		// Add the SubViewport to scene tree (required for rendering, but not as child of container)
		AddChild(_menuManager.Renderer.Viewport);
		// Create CanvasLayer to render 2D menu on top of 3D scene
		CanvasLayer canvasLayer = new()
		{
			Layer = 1, // Render above 3D scene
		};
		AddChild(canvasLayer);
		// Create margin background ColorRect that fills the entire window
		// This will be colored with the menu's border color
		_marginBackground = new ColorRect
		{
			Color = Colors.Black, // Default to black, will be updated by border color events
		};
		canvasLayer.AddChild(_marginBackground);
		// Create TextureRect to display the viewport texture with manual sizing
		_menuTextureRect = new TextureRect
		{
			Texture = _menuManager.Renderer.ViewportTexture,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.Scale, // Scale to fill the rect
			TextureFilter = Control.TextureFilterEnum.Nearest, // Sharp pixel-perfect rendering
		};
		canvasLayer.AddChild(_menuTextureRect);
		_lastWindowSize = Vector2I.Zero;
		UpdateFlatscreenMenuLayout();
		// Subscribe to border color change events
		_menuManager.Renderer.BordColorChanged += OnBordColorChanged;
		// Set initial margin color
		OnBordColorChanged(_menuManager.Renderer.CurrentBordColor);
		// Note: MenuManager constructor already calls RefreshMenu via NavigateToMenu
	}
	/// <summary>
	/// Called when the menu border color changes.
	/// Updates the margin background to match the new border color.
	/// </summary>
	/// <param name="color">New border color from the menu</param>
	private void OnBordColorChanged(Color color)
	{
		if (_marginBackground is not null)
			_marginBackground.Color = color;
	}
	/// <summary>
	/// Called each frame.
	/// Updates the menu system and reflows layout when the window is resized.
	/// </summary>
	/// <param name="delta">Time since last frame in seconds</param>
	public override void _Process(double delta)
	{
		UpdateFlatscreenMenuLayout();
		_menuManager?.Update(delta);
	}
	private void UpdateFlatscreenMenuLayout()
	{
		if (_marginBackground is null || _menuTextureRect is null)
			return;
		Vector2I windowSize = DisplayServer.WindowGetSize();
		if (windowSize == _lastWindowSize || windowSize.X <= 0 || windowSize.Y <= 0)
			return;
		_lastWindowSize = windowSize;
		_marginBackground.Size = windowSize;
		(Vector2 menuSize, Vector2 menuPosition) = CalculateAspectFit(windowSize, 4.0f / 3.0f);
		_menuTextureRect.Size = menuSize;
		_menuTextureRect.Position = menuPosition;
	}
	private static (Vector2 Size, Vector2 Position) CalculateAspectFit(Vector2I windowSize, float aspectRatio)
	{
		float windowAspect = (float)windowSize.X / windowSize.Y;
		if (windowAspect > aspectRatio)
		{
			Vector2 size = new(windowSize.Y * aspectRatio, windowSize.Y);
			return (size, new Vector2((windowSize.X - size.X) / 2f, 0f));
		}
		Vector2 letterboxedSize = new(windowSize.X, windowSize.X / aspectRatio);
		return (letterboxedSize, new Vector2(0f, (windowSize.Y - letterboxedSize.Y) / 2f));
	}
}
