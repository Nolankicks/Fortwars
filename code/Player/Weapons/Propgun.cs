using Sandbox.Events;

public sealed class Propgun : Item
{
	[Property] public Model Prop { get; set; }
	[Property] public string PropIdent { get; set; }
	public static bool FirstTime { get; set; } = true;
	public Angles PropRotation { get; set; }

	[Property] bool MustBeUp { get; set; } = false;
	protected override void OnStart()
	{
		if ( IsProxy || !FirstTime )
			return;

		var popup = new Popup();

		popup.Title = $"Press {Input.GetButtonOrigin( "menu" ).ToUpper()} to open the propgun menu";
		popup.Time = 8;

		PopupHolder.AddPopup( popup );

		Invoke( 2, () =>
		{
			var destroy = new Popup();
			destroy.Title = $"Press {Input.GetButtonOrigin( "destroy" ).ToUpper()} to destroy the prop you're looking at";
			destroy.Time = 8;

			PopupHolder.AddPopup( destroy );
		} );

		Invoke( 4, () =>
		{
			var reload = new Popup();
			reload.Title = $"Press {Input.GetButtonOrigin( "attack2" ).ToUpper()} to change the prop's rotation";
			reload.Time = 8;

			PopupHolder.AddPopup( reload );
		} );

		Invoke( 6, () =>
		{
			var attack1 = new Popup();
			attack1.Title = $"Press {Input.GetButtonOrigin( "reload" ).ToUpper()} to reset the rotation";
			attack1.Time = 8;

			PopupHolder.AddPopup( attack1 );
		} );

		FirstTime = false;
	}

	protected override void OnUpdate()
	{
		if ( IsProxy )
			return;

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
			var player = PlayerController.Local;

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



			ObjectPos = ObjectPos.SnapToGrid( 16, true, true, !tr.Hit );

			bool CanPlace = tr.Hit && tr.Distance > 32.0f && ObjectPos.Distance( GameObject.Root.WorldPosition ) > 32.0f;

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
				var currentTeam = player.TeamComponent?.Team;

				var gs = GameSystem.Instance;

				if ( !gs.IsValid() )
					return;

				switch ( currentTeam )
				{
					case Team.Red:
						if ( gs.RedProps.Count() >= gs.MaxProps )
							return;
						break;
					case Team.Blue:
						if ( gs.BlueProps.Count() >= gs.MaxProps )
							return;
						break;
					case Team.Green:
						if ( gs.GreenProps.Count() >= gs.MaxProps )
							return;
						break;
					case Team.Yellow:
						if ( gs.YellowProps.Count() >= gs.MaxProps )
							return;
						break;
				}

				GameObject.Dispatch( new WeaponAnimEvent( "b_attack", true ) );

				var gb = new GameObject();

				var renderer = gb.Components.Create<Prop>();

				renderer.Model = Prop;

				gb.WorldPosition = ObjectPos;
				gb.WorldRotation = PropRotation.SnapToGrid( 15 );

				var fortWarsProp = gb.Components.Create<FortwarsProp>();

				var team = player.TeamComponent;

				if ( team.IsValid() )
					fortWarsProp.Team = team.Team;

				fortWarsProp.Prop = renderer;
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

				if ( rb.IsValid() )
					SetStaticBodyType( rb );
			}
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

		var pc = PlayerController.Local;

		if ( pc.IsValid() )
			pc.CanMoveHead = true;

		var hud = Scene.GetAll<HUD>()?.FirstOrDefault();

		if ( hud.IsValid() )
		{
			foreach ( var panel in hud.Panel.ChildrenOfType<SpawnerMenu>() )
				panel.Delete();
		}
	}
}
