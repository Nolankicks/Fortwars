using Sandbox.Events;

public sealed class Propgun : Item
{
	[Property] public Model Prop { get; set; }
	[Property] public string PropIdent { get; set; }
	public static bool FirstTime { get; set; } = true;
	public Angles PropRotation { get; set; }
	[Property] bool MustBeUp { get; set; } = false;
	[Property] public bool UsingMouseInput { get; set; } = false;
	[Property] public SoundEvent ShootSound { get; set; }
	[RequireComponent] Viewmodel VModel { get; set; }

	protected override void OnStart()
	{
		if ( IsProxy || !FirstTime )
			return;

		var popup = new Popup();
		popup.Title = $"Press {Input.GetButtonOrigin( "menu" )?.ToUpper()} to open the propgun menu";
		popup.Time = 8;

		PopupHolder.AddPopup( popup );

		Invoke( 2, () =>
		{
			var destroy = new Popup();
			destroy.Title = $"Press {Input.GetButtonOrigin( "destroy" )?.ToUpper()} to destroy the prop you're looking at";
			destroy.Time = 8;

			PopupHolder.AddPopup( destroy );
		} );

		Invoke( 4, () =>
		{
			var reload = new Popup();
			reload.Title = $"Press {Input.GetButtonOrigin( "attack2" )?.ToUpper()} to change the prop's rotation";
			reload.Time = 8;

			PopupHolder.AddPopup( reload );
		} );

		Invoke( 6, () =>
		{
			var attack1 = new Popup();
			attack1.Title = $"Press {Input.GetButtonOrigin( "reload" )?.ToUpper()} to reset the rotation";
			attack1.Time = 8;

			PopupHolder.AddPopup( attack1 );
		} );

		Invoke( 8, () =>
		{
			var attack2 = new Popup();
			attack2.Title = $"Press {Input.GetButtonOrigin( "mouseprop" )?.ToUpper()} to toggle mouse input";
			attack2.Time = 8;

			PopupHolder.AddPopup( attack2 );
		} );

		FirstTime = false;
	}

	protected override void OnUpdate()
	{
		if ( IsProxy )
			return;

		if ( Input.Pressed( "mouseprop" ) )
		{
			Mouse.Visible = !Mouse.Visible;
			UsingMouseInput = !UsingMouseInput;

			PropRotation = Rotation.Identity;
		}

		if ( Input.Pressed( "menu" ) )
		{
			var hud = Scene.GetAll<HUD>()?.FirstOrDefault();

			var panel = new SpawnerMenu();

			panel.Propgun = this;

			panel.Position = hud.Panel.Box.Rect.Center - new Vector2( 100, 100 );

			if ( hud.IsValid() && hud.Panel?.ChildrenOfType<SpawnerMenu>()?.Count() == 0 )
				hud.Panel.AddChild( panel );
		}

		if ( Prop is not null )
		{
			if ( UsingMouseInput )
			{
				PlacePropMouse();
			}
			else
			{
				PlaceProp();
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

	public void PlacePropMouse()
	{
		var camera = Scene.Camera;

		if ( !camera.IsValid() )
			return;

		var tr = Scene.Trace
			.Box( Prop.Bounds.Rotate( PropRotation ) * 0.75f, Scene.Camera.ScreenPixelToRay( Mouse.Position ), 200f )
			.IgnoreGameObjectHierarchy( GameObject.Root )
			.Run();

		bool CanPlace = tr.Hit && tr.Distance > 32.0f && tr.EndPosition.Distance( GameObject.Root.WorldPosition ) > 32.0f && !tr.GameObject.Tags.Has( FW.Tags.NoBuild );

		var pos = tr.EndPosition.SnapToGrid( 16, true, true, !(tr.Hit && tr.Normal == Vector3.Up) );

		var gizmo = Gizmo.Draw.Model( Prop.ResourcePath );
		gizmo.ColorTint = Color.White.WithAlpha( 0.5f );
		gizmo.Rotation = PropRotation.SnapToGrid( 15 );
		gizmo.Position = pos;

		if ( !CanPlace )
		{
			gizmo.ColorTint = Color.Red.WithAlpha( 0.5f );
		}

		if ( tr.Hit && Input.Pressed( "destroy" ) && (tr.GameObject?.Root?.Components.TryGet<FortwarsProp>( out var prop, FindMode.EverythingInSelfAndDescendants ) ?? false) )
		{
			if ( prop.Invincible )
				return;

			tr.GameObject?.Root?.Destroy();
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

			if ( !FWPlayerController.Local.TeamComponent.IsValid() )
				return;

			SpawnProp( pos );
		}
	}

	public void PlaceProp()
	{
		var player = FWPlayerController.Local;

		if ( !player.IsValid() )
			return;

		Vector3 ObjectPos = player.Eye.WorldPosition + player.Eye.WorldRotation.Forward * 100.0f;

		var tr = Scene.Trace
			.Box( Prop.Bounds.Rotate( PropRotation ) * 0.75f, player.Eye.WorldPosition, player.Eye.WorldPosition + player.Eye.WorldRotation.Forward * 200.0f )
			.IgnoreGameObjectHierarchy( GameObject.Root )
			.Run();

		if ( tr.Hit )
		{
			ObjectPos = tr.EndPosition;
		}

		ObjectPos = ObjectPos.SnapToGrid( 16, true, true, !(tr.Hit && tr.Normal == Vector3.Up) );

		bool CanPlace = tr.Hit && tr.Distance > 32.0f && ObjectPos.Distance( GameObject.Root.WorldPosition ) > 32.0f && !tr.GameObject.Tags.Has( FW.Tags.NoBuild );

		var gizmo = Gizmo.Draw.Model( Prop.ResourcePath );
		gizmo.ColorTint = Color.White.WithAlpha( 0.5f );
		gizmo.Rotation = PropRotation.SnapToGrid( 15 );
		gizmo.Position = ObjectPos;


		if ( !CanPlace )
		{
			gizmo.ColorTint = Color.Red.WithAlpha( 0.5f );
		}

		player.CanMoveHead = !Input.Down( "attack2" );

		if ( tr.Hit && Input.Pressed( "destroy" ) && (tr.GameObject?.Root?.Components.TryGet<FortwarsProp>( out var prop, FindMode.EverythingInSelfAndDescendants ) ?? false) )
		{
			if ( prop.Invincible )
				return;

			tr.GameObject?.Root?.Destroy();
		}

		if ( Input.Down( "attack2" ) )
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

			SpawnProp( ObjectPos );
		}
	}

	public void SpawnProp( Vector3 ObjectPos )
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

		switch ( currentTeam )
		{
			case Team.Red:
				if ( gs.RedProps.Count() >= gs.MaxProps )
				{
					hud?.FlashPropsFailed();
					return;
				}
				break;
			case Team.Blue:
				if ( gs.BlueProps.Count() >= gs.MaxProps )
				{
					hud?.FlashPropsFailed();
					return;
				}
				break;
			case Team.Green:
				if ( gs.GreenProps.Count() >= gs.MaxProps )
				{
					hud?.FlashPropsFailed();
					return;
				}
				break;
			case Team.Yellow:
				if ( gs.YellowProps.Count() >= gs.MaxProps )
				{
					hud?.FlashPropsFailed();
					return;
				}
				break;
		}

		GameObject.Dispatch( new WeaponAnimEvent( "b_attack", true ) );

		var gb = new GameObject();

		var renderer = gb.Components.Create<Prop>();
		var fortWarsProp = gb.Components.Create<FortwarsProp>();

		renderer.Model = Prop;
		fortWarsProp.Health = renderer.Health;

		if ( fortWarsProp.Health == 0 )
			fortWarsProp.Invincible = true;

		// Break the prop into individuals so we can check for ModelPhysics later.
		renderer.Break();

		gb.WorldPosition = ObjectPos;
		gb.WorldRotation = PropRotation.SnapToGrid( 15 );

		if ( team.IsValid() )
			fortWarsProp.Team = team.Team;

		fortWarsProp.CanKill = false;

		if ( PropIdent is not null && (gs.ClassicIndents?.TryGetValue( PropIdent, out var propHealth ) ?? false) && propHealth > 0 )
			renderer.Health = propHealth;

		if ( gb.Components.TryGet<Rigidbody>( out var rb, FindMode.EverythingInSelfAndParent ) )
		{
			fortWarsProp.Rigidbody = rb;

			rb.PhysicsBody.BodyType = PhysicsBodyType.Static;
		}

		gb.Network.SetOwnerTransfer( OwnerTransfer.Takeover );
		gb.NetworkSpawn();

		// We need to do this for props with ModelPhysics since they start with MotionEnabled on, 
		// which breaks the positioning for the local client.
		if ( gb.Components.TryGet<ModelPhysics>( out var mdlPhys, FindMode.EnabledInSelf ) )
		{
			mdlPhys.MotionEnabled = false;
			Invoke( 0.1f, () => mdlPhys.MotionEnabled = true );
		}

		if ( rb.IsValid() )
			SetStaticBodyType( rb );
	}
}
