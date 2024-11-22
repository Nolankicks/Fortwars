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
	[Category( "Ammo" ), Property] public Weapon Weapon { get; set; }

	[Category( "Ammo" ), Property] float AmmoAngle { get; set; } = 180.0f;

	float HitmarkerPerc { get; set; } = 0.0f;

	private bool IsHeadshot { get; set; } = false;

	protected override void OnUpdate()
	{
		if ( IsProxy )
			return;

		var local = FWPlayerController.Local;
		if ( local.IsValid() && Dynamic )
			DynamicGap = (local.shrimpleCharacterController.Velocity.Length * 0.03f);

		DrawCrosshair( Scene.Camera.Hud );

		HitmarkerPerc = HitmarkerPerc.LerpTo( 0.0f, Time.Delta * 10.0f );
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


		DrawLine( hud, center + left * _gap, center + left * _end, Color.White, Size );
		DrawLine( hud, center - left * _gap, center - left * _end, Color.White, Size );


		DrawLine( hud, center + up * _gap, center + up * _end, Color.White, Size );
		if ( TopEnabled )
			DrawLine( hud, center - up * _gap, center - up * _end, Color.White, Size );

		if ( HitmarkerPerc > 0.1f )
		{
			float hitEnd = Gap + Length * HitmarkerPerc;
			Rotation hitAng = new Angles( 0, 45, 0 );
			Vector2 hitUp = hitAng.Forward;
			Vector2 hitSide = hitAng.Left;
			Color hitColor = IsHeadshot ? Color.Red : Color.White;
			hud.DrawLine( center + hitUp * Gap, center + hitUp * hitEnd, 1.0f, hitColor );
			hud.DrawLine( center - hitUp * Gap, center - hitUp * hitEnd, 1.0f, hitColor );
			hud.DrawLine( center + hitSide * Gap, center + hitSide * hitEnd, 1.0f, hitColor );
			hud.DrawLine( center - hitSide * Gap, center - hitSide * hitEnd, 1.0f, hitColor );
		}

		if ( !Weapon.IsValid() )
			return;

		var bullets = Weapon.MaxAmmo;

		float dist = 42.0f;
		for ( int i = 0; i < bullets; i++ )
		{
			float angle = (i + 0.5f) * (AmmoAngle / bullets);
			Vector2 dir = new Angles( 0, angle, 0 ).Forward;

			DrawLine( hud, center + dir * dist, center + dir * (dist + 8.0f), i + 1 > Weapon.Ammo ? Color.Gray.WithAlpha( 0.5f ) : Color.White.WithAlpha( 0.5f ), 3.0f );
		}
		float counterDist = 48.0f;
		hud.DrawText( new TextRendering.Scope( $"{Weapon.Ammo}/{Weapon.MaxAmmo}", Color.White, 16.0f, font: "IBMPlexMono", weight: 600 ), center + Vector2.Up * counterDist + Vector2.Left * counterDist );
	}



	void DrawLine( HudPainter hud, Vector2 start, Vector2 end, Color col, float size )
	{
		// Note the border is really buggy right now
		if ( BorderEnabled )
			hud.DrawLine( start, end, size + BorderSize, Color.Black, corners: Vector4.One * 4.0f );

		hud.DrawLine( start, end, size, col, corners: Vector4.One * 4.0f );
	}

	public void DoHitmarker( bool Kill )
	{
		Sound.Play( "hitmarker" );
		HitmarkerPerc = 1.0f;
		IsHeadshot = Kill;
	}
}
