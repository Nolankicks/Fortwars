using System;

namespace Facepunch;

[Title( "Scoped Weapon" ), Group( "Weapon Components" )]
public class ScopeWeaponComponent : Weapon
{
	[Property] public Material ScopeOverlay { get; set; }

	IDisposable renderHook;

	private float BlurLerp { get; set; } = 1.0f;

	private Angles LastAngles;
	private Angles AnglesLerp;
	[Property] private float AngleOffsetScale { get; set; } = 0.01f;

	public bool IsZooming = false;

	protected void StartZoom()
	{
		renderHook?.Dispose();
		renderHook = null;

		var camera = Scene.Camera;

		if ( ScopeOverlay is not null )
			renderHook = camera.AddHookAfterTransparent( "Scope", 100, RenderEffect );

		VModel.Renderer.GameObject.Enabled = false;

		IsZooming = true;

		FWPlayerController.Local.SpeedMult = 0.25f;
	}

	protected void EndZoom()
	{
		renderHook?.Dispose();

		AnglesLerp = new Angles();
		BlurLerp = 1.0f;
		VModel.Renderer.Set( "b_deploy_skip", true );
		VModel.Renderer.GameObject.Enabled = true;

		IsZooming = false;

		FWPlayerController.Local.SpeedMult = 1;
	}

	public override void OnEquip( OnItemEquipped onItemEquipped )
	{
		base.OnEquip( onItemEquipped );

		var player = FWPlayerController.Local;

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
		if ( Tags.Has( FW.Tags.Reloading ) ) return false;

		return true;
	}

	protected override void OnDisabled()
	{
		var hud = HUD.Instance;

		if ( hud.IsValid() )
			hud.ShowCrosshair = true;

		var player = FWPlayerController.Local;

		if ( !IsProxy && player.IsValid() )
			player.SetFov = false;

		EndZoom();

		VModel.Renderer.Set( "b_deploy_skip", false );
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
			hud.ShowCrosshair = false;

		if ( Input.Down( "attack2" ) )
		{
			StartZoom();
		}
		else
		{
			EndZoom();
		}


		var targetFov = IsZooming ? 45 : 0;

		var localPlayer = FWPlayerController.Local;
		localPlayer.OverrideFOV = targetFov;

		if ( IsZooming && !CanAim() )
		{
			EndZoom();
		}

		if ( IsZooming )
		{

			var cc = FWPlayerController.Local?.shrimpleCharacterController;

			float velocity = cc.Velocity.Length / 25.0f;
			float blur = 1.0f / (velocity + 1.0f);
			blur = MathX.Clamp( blur, 0.1f, 1.0f );

			if ( !cc.IsOnGround )
				blur = 0.1f;

			if ( blur > BlurLerp )
				BlurLerp = BlurLerp.LerpTo( blur, Time.Delta * 1.0f );
			else
				BlurLerp = BlurLerp.LerpTo( blur, Time.Delta * 10.0f );

			var angles = FWPlayerController.Local.EyeAngles;
			var delta = angles - LastAngles;

			AnglesLerp = AnglesLerp.LerpTo( delta, Time.Delta * 10.0f );
			LastAngles = angles;
		}
	}
};
