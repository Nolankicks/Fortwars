using Sandbox.UI;
using System;

public partial class Crosshair : Panel
{
	public bool ShowCrosshair { get; set; } = true;

	private Material CrosshairMat { get; set; }

	private RenderAttributes attributes { get; set; }

	private float stringLength { get; set; } = 16.0f;
	private float stringGap { get; set; } = 8.0f;

	private float borderWidth { get; set; } = 1.0f;
	private float borderRadius { get; set; } = 2.0f;

	public Color ColorTint { get; set; } = Color.White;

	private FWPlayerController local { get; set; }

	protected override void OnAfterTreeRender( bool firstTime )
	{
		base.OnAfterTreeRender( firstTime );
		attributes = new RenderAttributes();
		attributes.Set( "Texture", Texture.White );

		local = FWPlayerController.Local;
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

		float playerSpeed = local.shrimpleCharacterController.Velocity.Length * 0.05f;

		if ( !local.shrimpleCharacterController.IsOnGround )
			playerSpeed += 5.0f;

		if ( local.IsCrouching )
			playerSpeed -= 2.0f;


		var centerRect = Box.RectOuter;
		DrawSegment( centerRect );

		var rightRect = Box.RectOuter;
		rightRect.Left += centerRect.Width + stringGap + playerSpeed;
		rightRect.Width = stringLength;
		DrawSegment( rightRect );

		var leftRect = Box.RectOuter;
		leftRect.Left -= stringLength + stringGap + playerSpeed;
		leftRect.Width = stringLength;
		DrawSegment( leftRect );

		var topRect = Box.RectOuter;
		topRect.Top -= stringLength + stringGap + playerSpeed;
		topRect.Height = stringLength;
		DrawSegment( topRect );

		var bottomRect = Box.RectOuter;
		bottomRect.Top += centerRect.Height + stringGap + playerSpeed;
		bottomRect.Height = stringLength;
		DrawSegment( bottomRect );
	}

	void DrawSegment( Rect rect )
	{
		Graphics.DrawRoundedRectangle( rect, ColorTint, new Vector4( borderRadius ), new Vector4( borderWidth ), Color.Black );
	}
}
