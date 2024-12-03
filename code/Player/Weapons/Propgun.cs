using Sandbox.Events;
using System;
using System.Threading.Tasks;

public sealed class Propgun : Item
{
	enum Modes
	{
		P_PLACE,
	}

	[Property] public Model Prop { get; set; }
	[Property] public string PropIdent { get; set; }
	public static bool FirstTime { get; set; } = true;
	public Angles PropRotation { get; set; }
	[Property] bool MustBeUp { get; set; } = false;
	[Property] public bool UsingMouseInput { get; set; } = false;
	[Property] public SoundEvent ShootSound { get; set; }
	[Property] public PropResource CurrentProp { get; set; }
	[Property] public SkinnedModelRenderer WeaponRenderer { get; set; }

	public bool CanPlace { get; set; } = true;

	public bool HoldingObject { get; set; } = false;

	public GameObject HeldObject { get; set; }

	private Modes Mode { get; set; } = Modes.P_PLACE;

	public bool SnapToGrid { get; set; } = false;

	public bool UseBounds { get; set; } = false;

	[Property] public PropLevel Level { get; set; } = PropLevel.Metal;

	protected override void OnStart()
	{
		if ( IsProxy || !FirstTime )
			return;
		_ = DisplayControls();
		FirstTime = false;
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();

		if ( IsProxy )
			return;

		if ( !CanPlace )
			return;

		if ( Input.Pressed( "attack2" ) )
		{
			SwitchModes();
		}

		if ( Input.Pressed( "togglegrid" ) )
		{
			SnapToGrid = !SnapToGrid;
		}

		if ( Input.Pressed( "mouseprop" ) )
		{
			Mouse.Visible = !Mouse.Visible;
			UsingMouseInput = !UsingMouseInput;

			PropRotation = Rotation.Identity;
		}

		if ( Input.Pressed( "menu" ) && !HoldingObject )
		{
			Mouse.Visible = false;
			UsingMouseInput = false;

			var menu = PropRadialMenu.Instance;

			if ( menu.IsValid() )
			{
				menu.Visible = true;

				var player = FWPlayerController.Local;

				if ( player.IsValid() )
					Sound.Play( "weapon.deploy", player.WorldPosition );

				CanPlace = false;
			}
		}


		// Rotate the viewmodel around
		if ( UsingMouseInput )
		{
			WorldRotation = Rotation.Lerp( WorldRotation, Rotation.LookAt( Scene.Camera.ScreenPixelToRay( Mouse.Position ).Forward ), Time.Delta * 20.0f );
		}
		else
		{
			LocalRotation = Rotation.Identity;
		}

		if ( Mode == Modes.P_PLACE && CurrentProp is not null )
		{
			HandleProp();
		}
	}

	protected override void OnDisabled()
	{
		if ( IsProxy )
			return;

		PropRotation = Rotation.Identity;

		var pc = FWPlayerController.Local;

		if ( pc.IsValid() )
			pc.CanMoveHead = true;

		var hud = Scene.GetAll<HUD>()?.FirstOrDefault();

		if ( hud.IsValid() )
		{
			foreach ( var panel in hud.Panel.ChildrenOfType<SpawnerMenu>() )
				panel.Delete();
		}

		Mouse.Visible = false;
		UsingMouseInput = false;

		CanPlace = true;

		var menu = PropRadialMenu.Instance;

		if ( menu.IsValid() )
			menu.Visible = false;
	}

	public void HandleProp()
	{
		var player = FWPlayerController.Local;
		if ( !player.IsValid() )
			return;

		Vector3 ObjectPos;
		if ( UsingMouseInput )
			ObjectPos = Scene.Camera.WorldPosition + Scene.Camera.ScreenPixelToRay( Mouse.Position ).Forward * 200.0f;
		else
			ObjectPos = player.Eye.WorldPosition + player.Eye.WorldRotation.Forward * 400.0f;

		if ( CurrentProp is null || (CurrentProp?.BaseModel is null) )
			return;

		var model = CurrentProp.BaseModel;

		SceneTraceResult tr;

		if ( UseBounds )
			tr = Scene.Trace.Size( model.Bounds.Rotate( PropRotation ) ).Ray( player.Eye.WorldPosition, ObjectPos ).IgnoreGameObjectHierarchy( GameObject.Root ).Run();
		else
			tr = Scene.Trace.Ray( player.Eye.WorldPosition, ObjectPos ).IgnoreGameObjectHierarchy( GameObject.Root ).Run();

		if ( tr.Hit )
			ObjectPos = tr.EndPosition;

		if ( !UseBounds )
		{
			var rotatedBounds = model.Bounds.Rotate( PropRotation );

			ObjectPos = new Vector3( ObjectPos.x, ObjectPos.y, ObjectPos.z + rotatedBounds.Size.z / 2 ) + tr.Normal * 0.1f;
		}

		//ObjectPos = ObjectPos.SnapToGrid( 16, true, true, !(tr.Hit && tr.Normal == Vector3.Up) );

		bool CanPlace = tr.Hit && tr.Distance > 32.0f && ObjectPos.Distance( GameObject.Root.WorldPosition ) > 32.0f && (!tr.GameObject?.Tags.Has( FW.Tags.NoBuild ) ?? true);

		ShowPropPreview( ObjectPos, CurrentProp, CanPlace, tr.Normal, tr.Hit );

		player.CanMoveHead = !Input.Down( "RotateProp" );

		if ( tr.Hit && Input.Pressed( "destroy" ) && (tr.GameObject?.Root?.Components.TryGet<FortwarsProp>( out var prop, FindMode.EverythingInSelfAndDescendants ) ?? false) )
		{
			if ( prop.Invincible )
				return;

			if ( prop.Builder != player.Network.Owner?.DisplayName )
			{
				var popup = new Popup();
				popup.Title = "You can't destroy this prop because you don't own it!";
				popup.Time = 5;

				if ( Popup.HasPopup( popup ) )
					return;
				
				PopupHolder.AddPopup( popup );

				return;
			}

			switch ( prop.Level )
			{
				case PropLevel.Metal:
					player.MetalPropsLeft++;
					break;
				case PropLevel.Base:
					player.WoodPropsLeft++;
					break;
				case PropLevel.Steel:
					player.SteelPropsLeft++;
					break;
			}

			tr.GameObject?.Root?.Destroy();
		}

		if ( Input.Down( "RotateProp" ) )
		{
			var rot = Input.AnalogLook.WithRoll( 0 );

			PropRotation += new Angles( -rot.pitch, -rot.yaw, 0 );
		}

		if ( Input.Pressed( "reload" ) )
		{
			PropRotation = Rotation.Identity;
		}

		if ( CanPlace && Input.Pressed( "attack1" ) )
		{
			if ( !player.TeamComponent.IsValid() )
				return;

			if ( HeldObject is null )
			{
				SpawnProp( ObjectPos, tr.Normal, tr.Hit );
			}
			else
			{
				HeldObject.WorldPosition = ObjectPos;
				HeldObject.WorldRotation = PropRotation;

				HeldObject = null;
				HoldingObject = false;
			}
		}
	}

	public void SpawnProp( Vector3 ObjectPos, Vector3 Normal, bool Hit )
	{
		var player = FWPlayerController.Local;

		if ( !player.IsValid() )
			return;

		var team = player.TeamComponent;

		var gs = GameSystem.Instance;

		if ( !gs.IsValid() || !team.IsValid() )
			return;

		if ( OverTeamPropLimit() )
		{
			Sound.Play( "ui.downvote" );
			return;
		}

		if ( ShootSound is not null )
		{
			var sound = Sound.Play( ShootSound, WorldPosition );

			if ( sound.IsValid() )
				sound.Volume = 0.5f;
		}

		GameObject.Dispatch( new WeaponAnimEvent( "b_attack", true ) );

		GameObject gb = null;

		if ( CurrentProp.PrefabOverride.IsValid() )
		{
			gb = CurrentProp.PrefabOverride.Clone();
		}
		else
			gb = GameObject.Clone( "prefabs/PropBase.prefab" );

		var fortWarsProp = gb.Components.Get<FortwarsProp>();

		fortWarsProp.IsBuilding = true;

		if ( Network.Owner is not null )
			fortWarsProp?.SetupObject( CurrentProp, team.Team, Level, Network.Owner.DisplayName );

		var local = FWPlayerController.Local;

		switch ( Level )
		{
			case PropLevel.Metal:
				local.MetalPropsLeft--;
				break;
			case PropLevel.Base:
				local.WoodPropsLeft--;
				break;
			case PropLevel.Steel:
				local.SteelPropsLeft--;
				break;
		}

		gb.WorldPosition = SnapToGrid ? ObjectPos.SnapToGrid( 16, true, true, !(Hit && Normal == Vector3.Up) ) : ObjectPos;
		gb.WorldRotation = PropRotation.SnapToGrid( 15 );

		gb.Network.SetOwnerTransfer( OwnerTransfer.Takeover );
		gb.NetworkSpawn();

		//// We need to do this for props with ModelPhysics since they start with MotionEnabled on, 
		//// which breaks the positioning for the local client.
		//if ( gb.Components.TryGet<ModelPhysics>( out var mdlPhys, FindMode.EnabledInSelf ) )
		//{
		//	mdlPhys.MotionEnabled = false;
		//	Invoke( 0.1f, () => mdlPhys.MotionEnabled = true );
		//}

		//HoldingObject = false;
	}

	void ShowPropPreview( Vector3 pos, PropResource prop, bool canPlace, Vector3 Normal, bool Hit )
	{
		Model model;

		switch ( Level )
		{
			case PropLevel.Metal:
				model = prop.MetalModel;
				break;
			case PropLevel.Base:
				model = prop.BaseModel;
				break;
			case PropLevel.Steel:
				model = prop.SteelModel;
				break;
			default:
				model = prop.BaseModel;
				break;
		}

		var gizmo = Gizmo.Draw.Model( model.ResourcePath );
		gizmo.ColorTint = Color.White.WithAlpha( 0.5f );
		gizmo.Rotation = PropRotation.SnapToGrid( 15 );

		var local = FWPlayerController.Local;

		if ( !local.IsValid() || (!local?.TeamComponent?.IsValid() ?? false) )
			return;

		gizmo.SetMaterialGroup( local.TeamComponent.Team switch
		{
			Team.Red => "red",
			Team.Blue => "default",
			_ => "default"
		} );

		if ( UseBounds )
			gizmo.Position = SnapToGrid ? pos.SnapToGrid( 16, true, true, !(Hit && Normal == Vector3.Up) ) : pos;
		else
			gizmo.Position = SnapToGrid ? pos.SnapToGrid( 16 ) : pos;

		if ( !canPlace )
		{
			gizmo.ColorTint = Color.Red.WithAlpha( 0.5f );
		}
	}

	bool OverTeamPropLimit()
	{
		var local = FWPlayerController.Local;

		if ( !local.IsValid() )
			return true;

		switch ( Level )
		{
			case PropLevel.Metal:
				return local.MetalPropsLeft <= 0;
			case PropLevel.Base:
				return local.WoodPropsLeft <= 0;
			case PropLevel.Steel:
				return local.SteelPropsLeft <= 0;
		}

		return true;
	}

	private async Task DisplayControls()
	{
		float delay = 2.0f;
		List<string> messages = new List<string>()
		{
			$"Press {Input.GetButtonOrigin( "menu" )?.ToUpper()} to open the propgun menu",
			$"Press {Input.GetButtonOrigin( "destroy" )?.ToUpper()} to destroy the prop you're looking at",
			$"Press {Input.GetButtonOrigin( "attack2" )?.ToUpper()} to change the prop's rotation",
			$"Press {Input.GetButtonOrigin( "reload" )?.ToUpper()} to reset the rotation",
			//$"Press {Input.GetButtonOrigin( "mouseprop" )?.ToUpper()} to toggle mouse input"
		};

		foreach ( var message in messages )
		{
			var popup = new Popup();
			popup.Title = message;
			popup.Time = 8;

			PopupHolder.AddPopup( popup );

			await Task.DelaySeconds( delay );
		}
	}

	void SwitchModes()
	{
		/*if ( Mode == Modes.P_PLACE )
		{
			Mode = Modes.P_MOVE;

			//Can't use viewmodel in this case since our weapon is not the viewmodels renderer
			if ( WeaponRenderer.IsValid() )
				WeaponRenderer.Tint = Color.Cyan;
		}
		else
		{
			Mode = Modes.P_PLACE;

			if ( WeaponRenderer.IsValid() )
				WeaponRenderer.Tint = Color.White;
		}*/
	}

	public string GetModeString()
	{
		if ( Mode == Modes.P_PLACE )
			return "Place";
		else
			return "Move";
	}
}
