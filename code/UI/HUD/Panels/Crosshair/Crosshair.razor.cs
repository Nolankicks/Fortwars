using Sandbox.UI;
using System;

public partial class Crosshair : Panel
{
	public class Properties
	{
		public bool IsDynamic { get; set; } = true;
		public bool ShowTop { get; set; } = true;
		public int Length { get; set; } = 16;
		public int Gap { get; set; } = 8;
		public int BorderThickness { get; set; } = 1;

		public int BorderRadius { get; set; } = 4;

		public Color InnerColor { get; set; } = Color.White;
		public Color BorderColor { get; set; } = Color.Black;

	}

	public bool ShowCrosshair { get; set; } = true;
	private Material CrosshairMat { get; set; }

	private RenderAttributes attributes { get; set; }

	private float borderWidth { get; set; } = 1.0f;
	private float borderRadius { get; set; } = 2.0f;

	public Color ColorTint { get; set; } = Color.Red;

	private FWPlayerController local { get; set; }

	public static Crosshair Instance { get; set; }
	public Properties Config { get; set; }

	public const string FilePath = "crosshair.json";

	public float GapAddition { get; set; }

	public float TimeToReturn { get; set; } = 2.0f;

	public Crosshair()
	{
		attributes = new RenderAttributes();
		attributes.Set( "Texture", Texture.White );

		Instance = this;
		Config = GetActiveConfig();
	}

	protected override void OnAfterTreeRender( bool firstTime )
	{
		local = FWPlayerController.Local;
	}

	public static Properties GetActiveConfig()
	{
		return FileSystem.Data.ReadJson<Properties>( FilePath ) ?? Instance?.Config ?? new Properties();
	}

	public override void Tick()
	{
		base.Tick();
		GapAddition = GapAddition.LerpTo( 0, Time.Delta * TimeToReturn );
	}

	protected override int BuildHash()
	{
		var hash = new HashCode();
		hash.Add( Time.Now );
		return hash.ToHashCode();
	}

	public override void DrawBackground( ref RenderState state )
	{
		base.DrawBackground( ref state );

		if ( !local.IsValid() )
			return;

		Log.Info( ShowCrosshair );

		if ( !ShowCrosshair )
			return;

		var centerRect = Box.RectOuter;
		DrawSegment( centerRect );

		var rightRect = Box.RectOuter;
		rightRect.Left += centerRect.Width + GetGap();
		rightRect.Width = Config.Length;
		DrawSegment( rightRect );

		var leftRect = Box.RectOuter;
		leftRect.Left -= Config.Length + GetGap();
		leftRect.Width = Config.Length;
		DrawSegment( leftRect );

		if ( Config.ShowTop )
		{
			var topRect = Box.RectOuter;
			topRect.Top -= Config.Length + GetGap();
			topRect.Height = Config.Length;
			DrawSegment( topRect );
		}

		var bottomRect = Box.RectOuter;
		bottomRect.Top += centerRect.Height + GetGap();
		bottomRect.Height = Config.Length;
		DrawSegment( bottomRect );
	}

	int GetGap()
	{
		int playerSpeed = (int)(local.shrimpleCharacterController.Velocity.Length * 0.03f);

		if ( !local.shrimpleCharacterController.IsOnGround )
			playerSpeed += 5;

		if ( local.IsCrouching )
			playerSpeed -= 2;

		return Config.Gap + playerSpeed + (int)GapAddition;
	}

	void DrawSegment( Rect rect )
	{
		Graphics.DrawRoundedRectangle( rect, Config.InnerColor, new Vector4( Config.BorderRadius ), new Vector4( Config.BorderThickness ), Config.BorderColor );
	}
}
