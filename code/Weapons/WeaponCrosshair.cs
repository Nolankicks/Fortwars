using Sandbox.Rendering;

public sealed class WeaponCrosshair : Component
{
	[Property, Range( 1.0f, 8.0f, clamped: false )] float Size { get; set; } = 4.0f;
	[Property] bool BorderEnabled { get; set; } = true;
	[Property, HideIf( "BorderEnabled", false )] float BorderSize { get; set; } = 1.0f;

	[Property] float Gap { get; set; } = 4.0f;
	[Property] float Length { get; set; } = 16.0f;

	[Property] bool Dynamic { get; set; } = true;
	[Property] bool TopEnabled { get; set; } = true;

	private float DynamicGap { get; set; } = 0.0f;

	protected override void OnUpdate()
	{
		if ( IsProxy )
			return;

		var local = FWPlayerController.Local;
		if ( local.IsValid() && Dynamic )
			DynamicGap = (local.shrimpleCharacterController.Velocity.Length * 0.03f);

		DrawCrosshair( Scene.Camera.Hud );
	}

	void DrawCrosshair( HudPainter hud )
	{
		var center = Screen.Size * 0.5f;

		if ( BorderEnabled )
			hud.DrawCircle( center, Size + BorderSize, Color.Black );

		hud.DrawCircle( center, Size, Color.White );

		Rotation rot = new Angles( 0, 90, 0 );
		Vector2 up = rot.Forward;
		Vector2 left = rot.Left;

		float _gap = Gap + DynamicGap;

		float _end = _gap + Length;


		DrawLine( hud, center + left * _gap, center + left * _end );
		DrawLine( hud, center - left * _gap, center - left * _end );


		DrawLine( hud, center + up * _gap, center + up * _end );
		if ( TopEnabled )
			DrawLine( hud, center - up * _gap, center - up * _end );
	}

	void DrawLine( HudPainter hud, Vector2 start, Vector2 end )
	{
		hud.DrawLine( start, end, Size, Color.White, corners: Vector4.One * 4.0f );
	}
}
