using Sandbox.Events;
using System;
using System.Threading.Tasks;

public sealed class Propgun : Item
{
	enum Modes
	{
		P_PLACE,
		P_MOVE
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

	public bool HoldingObject { get; set; } = false;

	public GameObject HeldObject { get; set; }

	private Modes Mode { get; set; } = Modes.P_PLACE;

	public bool SnapToGrid { get; set; } = false;

	public bool UseBounds { get; set; } = true;

	protected override void OnStart()
	{
		if ( IsProxy || !FirstTime )
			return;
		_ = DisplayControls();
		FirstTime = false;
	}

	protected override void OnUpdate()
	{
		if ( IsProxy )
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

		if ( Input.Pressed( "voice" ) )
		{
			UseBounds = !UseBounds;
		}

		if ( Input.Pressed( "menu" ) && !HoldingObject )
		{
			var hud = Scene.GetAll<HUD>()?.FirstOrDefault();

			Mouse.Visible = false;
			UsingMouseInput = false;

			var panel = new SpawnerMenu();

			panel.Propgun = this;

			if ( hud.IsValid() && hud.Panel?.ChildrenOfType<SpawnerMenu>()?.Count() == 0 )
				hud.Panel.AddChild( panel );
			else (hud?.Panel?.ChildrenOfType<SpawnerMenu>()?.FirstOrDefault())?.Delete();
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

		if ( Mode == Modes.P_MOVE && !HoldingObject )
		{
			if ( Input.Pressed( "attack1" ) )
			{
				var player = FWPlayerController.Local;
				if ( !player.IsValid() )
					return;

				var tr = Scene.Trace.Ray( player.Eye.WorldPosition, player.Eye.WorldPosition + player.Eye.WorldRotation.Forward * 400.0f ).WithoutTags( FW.Tags.NoBuild, FW.Tags.Player ).Run();
				if ( tr.Hit && tr.GameObject.Components.TryGet<FortwarsProp>( out var fwProp ) )
				{
					if ( fwProp.Resource == null )
						return;
					HoldingObject = true;
					var res = fwProp.Resource;
					CurrentProp = res;
					HeldObject = tr.GameObject;
					tr.Component.Enabled = false;
				}
			}
			return;
		}

		if ( (Mode == Modes.P_PLACE && CurrentProp is not null) || (Mode == Modes.P_MOVE && HeldObject.IsValid()) )
		{
			HandleProp();
		}
	}


	[Broadcast]
	public void SetStaticBodyType( Rigidbody rb )
	{
		rb.PhysicsBody.BodyType = PhysicsBodyType.Static;
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

		var model = CurrentProp.Model;

		SceneTraceResult tr;

		if ( UseBounds )
			tr = Scene.Trace.Size( model.Bounds.Rotate( PropRotation ) ).Ray( player.Eye.WorldPosition, ObjectPos ).IgnoreGameObjectHierarchy( GameObject.Root ).Run();
		else
			tr = Scene.Trace.Ray( player.Eye.WorldPosition, ObjectPos ).IgnoreGameObjectHierarchy( GameObject.Root ).Run();

		if ( tr.Hit )
		{
			ObjectPos = tr.HitPosition;

			if ( !UseBounds )
				ObjectPos = new Vector3( ObjectPos.x, ObjectPos.y, ObjectPos.z + model.Bounds.Size.z / 2 );
		}

		//ObjectPos = ObjectPos.SnapToGrid( 16, true, true, !(tr.Hit && tr.Normal == Vector3.Up) );

		bool CanPlace = tr.Hit && tr.Distance > 32.0f && ObjectPos.Distance( GameObject.Root.WorldPosition ) > 32.0f && !tr.GameObject.Tags.Has( FW.Tags.NoBuild );

		ShowPropPreview( ObjectPos, CurrentProp, CanPlace, tr.Normal, tr.Hit );

		player.CanMoveHead = !Input.Down( "RotateProp" );

		if ( tr.Hit && Input.Pressed( "destroy" ) && (tr.GameObject?.Root?.Components.TryGet<FortwarsProp>( out var prop, FindMode.EverythingInSelfAndDescendants ) ?? false) )
		{
			if ( prop.Invincible )
				return;

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
			if ( ShootSound is not null )
			{
				var sound = Sound.Play( ShootSound, WorldPosition );

				if ( sound.IsValid() )
					sound.Volume = 0.5f;
			}

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
				var rb = HeldObject.Components.Get<Rigidbody>( FindMode.EverythingInSelf );
				rb.Enabled = true;
				SetStaticBodyType( rb );
				HeldObject.Network.Refresh();


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

		var currentTeam = team.Team;

		var hud = Scene.GetAll<HUD>()?.FirstOrDefault();

		if ( OverTeamPropLimit( currentTeam, hud, gs ) )
		{
			hud?.FlashPropsFailed();
			return;
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


		fortWarsProp.SetupObject( CurrentProp, team.Team );


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
		var gizmo = Gizmo.Draw.Model( prop.Model.ResourcePath );
		gizmo.ColorTint = Color.White.WithAlpha( 0.5f );
		gizmo.Rotation = PropRotation.SnapToGrid( 15 );

		if ( UseBounds )
			gizmo.Position = SnapToGrid ? pos.SnapToGrid( 16, true, true, !(Hit && Normal == Vector3.Up) ) : pos;
		else
			gizmo.Position = SnapToGrid ? pos.SnapToGrid( 16) : pos;

		if ( !canPlace )
		{
			gizmo.ColorTint = Color.Red.WithAlpha( 0.5f );
		}
	}

	bool OverTeamPropLimit( Team currentTeam, HUD hud, GameSystem gs )
	{
		switch ( currentTeam )
		{
			case Team.Red:
				if ( gs.RedProps.Count() >= gs.MaxProps )
					return true;
				break;
			case Team.Blue:
				if ( gs.BlueProps.Count() >= gs.MaxProps )
					return true;
				break;
			case Team.Green:
				if ( gs.GreenProps.Count() >= gs.MaxProps )
					return true;
				break;
			case Team.Yellow:
				if ( gs.YellowProps.Count() >= gs.MaxProps )
					return true;
				break;
		}
		return false;
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
		if ( Mode == Modes.P_PLACE )
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
		}
	}

	public string GetModeString()
	{
		if ( Mode == Modes.P_PLACE )
			return "Place";
		else
			return "Move";
	}
}
