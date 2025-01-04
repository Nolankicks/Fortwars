public sealed class Flag : Item
{
	[Property] public GameObject DroppedFlagPrefab { get; set; }
	[Property, Sync] public Team Owner { get; set; }
	[Sync] public bool SpawnNewFlag { get; set; } = true;
	[Property, Sync] public SkinnedModelRenderer FlagRenderer { get; set; }
	[Sync] public HighlightOutline HighlightOutline { get; set; }

	protected override void OnDisabled()
	{
		base.OnDisabled();

		if ( HighlightOutline.IsValid() )
		{
			HighlightOutline.Destroy();
		}

		if ( IsProxy )
			return;

		DropFlag();

		GameObject?.Root?.Network?.Refresh();
	}

	protected override void OnStart()
	{
		base.OnStart();

		HighlightOutline = GameObject.Root.Components.GetOrCreate<HighlightOutline>();

		if ( IsProxy )
			return;

		Scene.GetAll<CTFTrigger>()?.FirstOrDefault( x => x.Team == Owner ).OnTeamFlagPickup();
	}

	protected override void OnUpdate()
	{
		if ( IsProxy )
			return;

		if ( FlagRenderer.IsValid() )
			FlagRenderer.Tint = HUD.GetColor( Owner ).Rgb;

		if ( Input.Pressed( "menu" ) )
		{
			GameObject.Enabled = false;
		}
	}

	protected override void OnPreRender()
	{
		if ( HighlightOutline.IsValid() )
		{
			HighlightOutline.Enabled = IsProxy;
		}
	}

	[Rpc.Owner]
	public void SetOwner( Team team )
	{
		Owner = team;
	}

	[Rpc.Owner]
	public void SetSpawnNewFlag( bool value )
	{
		SpawnNewFlag = value;
	}

	public void DropFlag()
	{
		if ( IsProxy )
			return;

		if ( !SpawnNewFlag )
			return;

		var local = FWPlayerController.Local;

		if ( !local.IsValid() || (!local?.Inventory.IsValid() ?? true) )
			return;

		if ( local.Inventory.CurrentItem == GameObject )
			Log.Info( "Flag disabled" );

		local.SetHasFlag( false );

		if ( SpawnNewFlag )
		{
			//We need this, but I don't think we should need it
			var clone = DroppedFlagPrefab.Clone( local.WorldPosition );

			if ( clone.Components.TryGet<DroppedFlag>( out var droppedFlag ) )
			{
				droppedFlag.TeamFlag = Owner;
			}

			clone.NetworkSpawn( null );
		}

		local.Inventory.RemoveItem( GameObject, true );

		FWPlayerController.ClearHoldRenderer( local.HoldRenderer );
	}
}

public sealed class DroppedFlag : Component, Component.ITriggerListener, Component.IDamageable
{
	[Property, Sync] public Team TeamFlag { get; set; }

	[Property] NavMarker Marker { get; set; }

	[Property] public SkinnedModelRenderer FlagRenderer { get; set; }

	TimeSince CreationDelay { get; set; }

	protected override void OnStart()
	{
		CreationDelay = 0.0f;

		if ( !IsProxy )
		{
			Network.SetOwnerTransfer( OwnerTransfer.Takeover );
			Network.SetOrphanedMode( NetworkOrphaned.ClearOwner );
		}

		if ( FlagRenderer.IsValid() )
		{
			FlagRenderer.MaterialGroup = TeamFlag switch
			{
				Team.Red => "red",
				Team.Blue => "blue",
				_ => "default"
			};
		}

		Marker.Tint = HUD.GetColor( TeamFlag ).Rgb;
	}

	void ITriggerListener.OnTriggerEnter( Collider other )
	{
		if ( IsProxy || CreationDelay < 0.5f )
			return;

		if ( other.GameObject.Components.TryGet<FWPlayerController>( out var playerController, FindMode.EverythingInSelfAndParent ) )
		{
			var flag = ResourceLibrary.Get<WeaponData>( "weapondatas/flag.weapons" );

			var inv = playerController.Inventory;

			var teamComponent = playerController.TeamComponent;

			if ( teamComponent.IsValid() && teamComponent.Team == TeamFlag )
				return;

			if ( inv.IsValid() && !inv.CanPickUp )
				return;

			inv.DisableAll();

			var config = new CloneConfig();
			config.StartEnabled = false;

			var obj = flag.WeaponPrefab.Clone( config );

			if ( obj.Components.TryGet<Flag>( out var preSpawnFlag, FindMode.DisabledInSelf ) )
			{
				preSpawnFlag.Owner = TeamFlag;
			}

			obj.NetworkSpawn( false, playerController.Network.Owner );

			if ( obj.IsValid() )
				inv.AddItem( obj, flag, enabled: true, changeIndex: true );

			PopupHolder.BroadcastPopup( $"{teamComponent.Team} grabbed the flag!", 5 );

			var flagComponent = obj.Components.Get<Flag>();

			if ( flagComponent.IsValid() )
			{
				flagComponent.DestroyViewmodelRenderers();
			}

			playerController.SetHasFlag( true );

			GameObject.Destroy();
		}
	}

	[Rpc.Owner]
	public void ResetPos()
	{
		var spawnPoint = Scene.GetAll<FlagSpawn>()?.FirstOrDefault( x => x.Team == TeamFlag );

		if ( spawnPoint.IsValid() )
		{
			Transform.ClearInterpolation();

			WorldTransform = spawnPoint.WorldTransform;
		}
	}

	
	[Sync, Property, ReadOnly] public int Health { get; set; } = 100;

	void IDamageable.OnDamage( in DamageInfo damage )
	{
		Health -= (int)damage.Damage;

		if ( Health <= 0 )
		{
			ResetPos();

			PopupHolder.BroadcastPopup( $"{TeamFlag} flag has been returned!", 5 );

			Health = 100;
		}
	}
}

[Title( "Flag Capture Zone" )]
public sealed class CTFTrigger : Component, Component.ITriggerListener
{
	[Header( "Should be the opposite team of the side its on" )]
	[Property, Sync] public Team Team { get; set; }

	GameObject Marker { get; set; }

	void ITriggerListener.OnTriggerEnter( Collider other )
	{
		var flag = ResourceLibrary.Get<WeaponData>( "weapondatas/flag.weapons" );

		var local = other.GameObject.Components.Get<FWPlayerController>( FindMode.EverythingInSelfAndParent );

		if ( !local.IsValid() )
			return;

		var gs = GameSystem.Instance;

		if ( !gs.IsValid() || flag is null || !local.TeamComponent.IsValid() )
			return;

		if ( local.TeamComponent.Team == Team )
			return;

		if ( local.Inventory.IsValid() && local.Components.TryGet<Flag>( out var flagComponent, FindMode.EverythingInSelfAndDescendants ) )
		{
			flagComponent.SetSpawnNewFlag( false );

			local.Inventory.RemoveItem( flagComponent.GameObject );

			gs.AddFlagCapture( local.TeamComponent.Team );

			PopupHolder.BroadcastPopup( $"{local.TeamComponent.Team} captured the flag!", 5 );

			OnTeamFlagCaptured();

			Scene?.GetAll<TeamComponent>()?.ToList()?.ForEach( x => x.PlayFlagCapturedSound( Team ) );
		}
	}

	// Team is the team who just picked up the flag
	[Rpc.Broadcast]
	public void OnTeamFlagPickup()
	{
		var local = FWPlayerController.Local;

		if ( !local.IsValid() )
			return;

		if ( Marker.IsValid() )
			return;

		// Let's enable this on the client side
		if ( local.TeamComponent.Team != Team )
		{
			Marker = new GameObject();
			Marker.SetParent( GameObject );
			Marker.WorldPosition = WorldPosition;
			var marker = Marker.Components.Create<NavMarker>();
			marker.Tint = HUD.GetColor( Team ).Rgb;
			marker.Text = "RETURN";
		}
	}

	[Rpc.Broadcast]
	public void OnTeamFlagCaptured()
	{
		var particles = GameObject.Clone( "prefabs/effects/flagcapture.prefab" );
		particles.WorldPosition = WorldPosition;
		particles.WorldRotation = new Angles( -90, 0, 0 );
		particles.Transform.ClearInterpolation();

		var local = FWPlayerController.Local;

		if ( !local.IsValid() )
			return;

		if ( !Marker.IsValid() )
			return;

		// Let's enable this on the client side
		if ( local.TeamComponent.Team != Team )
		{
			Marker.Destroy();
		}
	}
}

public sealed class FlagSpawn : Component
{
	[Property] public Team Team { get; set; }

	protected override void DrawGizmos()
	{
		Model model = Model.Load( "models/flag_blue/flag_blue.vmdl" );
		Gizmo.Hitbox.Model( model );
		Gizmo.Draw.Color = TeamSpawnPoint.GetTeamColor( Team );
		SceneObject sceneObject = Gizmo.Draw.Model( model );
	}
}
