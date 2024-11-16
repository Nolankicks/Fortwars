using Sandbox.Rendering;

public sealed class WeaponCrosshair : Component
{
	protected override void OnUpdate()
	{
		if ( IsProxy )
			return;

		DrawCrosshair( Scene.Camera.Hud );
	}

	void DrawCrosshair( HudPainter hud )
	{
		hud.DrawCircle( Screen.Size / 2, 8.0f, Color.White );



		hud.DrawText( new TextRendering.Scope( "Hello!", Color.Red, 32 ), Screen.Width * 0.5f );
	}
}
