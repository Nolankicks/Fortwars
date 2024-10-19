using System;

namespace Facepunch;

[Title( "2D Scope" ), Group( "Weapon Components" )]
public class ScopeWeaponComponent : Weapon
{
	[Property] public Material ScopeOverlay { get; set; }
	[Property] public SoundEvent ZoomSound { get; set; }
	[Property] public SoundEvent UnzoomSound { get; set; }

	IDisposable renderHook;

	public override bool CanFire => IsZooming;
	public bool IsZooming => Input.Down( "attack2" );
	private float BlurLerp { get; set; } = 1.0f;

	private Angles LastAngles;
	private Angles AnglesLerp;
	[Property] private float AngleOffsetScale { get; set; } = 0.01f;

	protected void StartZoom()
	{
		renderHook?.Dispose();
		renderHook = null;

		var camera = Scene.Camera;

		if ( ScopeOverlay is not null )
			renderHook = camera.AddHookAfterTransparent( "Scope", 100, RenderEffect );
	}

	protected void EndZoom()
	{
		renderHook?.Dispose();

		AnglesLerp = new Angles();
		BlurLerp = 1.0f;
	}

	public override void OnEquip( OnItemEquipped onItemEquipped )
	{
		base.OnEquip( onItemEquipped );

		var player = PlayerController.Local;

		if ( !IsProxy && player.IsValid() )
			player.SetFov = false;
	}

	public void RenderEffect( SceneCamera camera )
	{
		RenderAttributes attrs = new RenderAttributes();

		attrs.Set( "BlurAmount", BlurLerp );
		attrs.Set( "Offset", new Vector2( AnglesLerp.yaw, -AnglesLerp.pitch ) * AngleOffsetScale );

		Graphics.Blit( ScopeOverlay, attrs );
	}

	protected virtual bool CanAim()
	{
		if ( Tags.Has( "reloading" ) ) return false;

		return true;
	}

	protected override void OnDisabled()
	{
		var hud = HUD.Instance;

		if ( hud.IsValid() )
			hud.ShowCrosshair = true;

		var player = PlayerController.Local;

		if ( !IsProxy && player.IsValid() )
			player.SetFov = false;

		EndZoom();
		base.OnDisabled();
	}

	protected override void OnParentChanged( GameObject oldParent, GameObject newParent )
	{
		base.OnParentChanged( oldParent, newParent );
		EndZoom();
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();

		if ( IsProxy )
			return;

		var hud = HUD.Instance;

		if ( hud.IsValid() )
			hud.ShowCrosshair = !IsZooming;

		if ( Input.Down( "attack2" ) )
		{
			StartZoom();
		}
		else
		{
			EndZoom();
		}

		if ( !IsZooming )
			return;

		var camera = Scene.Camera;
		if ( !camera.IsValid() )
			return;

		var targetFov = IsZooming ? 45 : Preferences.FieldOfView;

		camera.FieldOfView = targetFov;

		if ( !CanAim() )
		{
			EndZoom();
		}

		{
			var cc = PlayerController.Local?.shrimpleCharacterController;

			float velocity = cc.Velocity.Length / 25.0f;
			float blur = 1.0f / (velocity + 1.0f);
			blur = MathX.Clamp( blur, 0.1f, 1.0f );

			if ( !cc.IsOnGround )
				blur = 0.1f;

			if ( blur > BlurLerp )
				BlurLerp = BlurLerp.LerpTo( blur, Time.Delta * 1.0f );
			else
				BlurLerp = BlurLerp.LerpTo( blur, Time.Delta * 10.0f );

			var angles = PlayerController.Local.EyeAngles;
			var delta = angles - LastAngles;

			AnglesLerp = AnglesLerp.LerpTo( delta, Time.Delta * 10.0f );
			LastAngles = angles;
		}
	}
};
