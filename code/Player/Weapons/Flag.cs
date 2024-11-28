using Sandbox;
using Sandbox.Events;

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

		if ( FlagRenderer.IsValid() )
			FlagRenderer.Tint = HUD.GetColor( Owner ).Rgb;
	}

	protected override void OnUpdate()
	{
		if ( IsProxy )
			return;

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

	[Authority]
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

		if ( SpawnNewFlag )
		{
			var clone = DroppedFlagPrefab.Clone( WorldPosition + local.EyeAngles.Forward * 100 );

			if ( clone.Components.TryGet<DroppedFlag>( out var droppedFlag ) )
			{
				droppedFlag.TeamFlag = local.TeamComponent.Team == Team.Red ? Team.Blue : Team.Red;
			}

			clone.NetworkSpawn( null );
		}

		local.Inventory.RemoveItem( GameObject, false );

		FWPlayerController.ClearHoldRenderer( local.HoldRenderer );
	}
}

public sealed class DroppedFlag : Component, Component.ITriggerListener
{
	[Property, Sync] public Team TeamFlag { get; set; }

	protected override void OnStart()
	{
		foreach ( var modelRenderer in GameObject.GetComponentsInChildren<ModelRenderer>() )
		{
			modelRenderer.Tint = HUD.GetColor( TeamFlag ).Rgb;
		}
	}

	void ITriggerListener.OnTriggerEnter( Collider other )
	{
		if ( IsProxy )
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

			if ( flag is not null )
				inv.AddItem( flag, enabled: true, changeIndex: true );

			PopupHolder.BroadcastPopup( $"{teamComponent.Team} grabbed the flag!", 5 );

			if ( inv.Components.TryGet<Flag>( out var flagComponent, FindMode.EverythingInSelfAndDescendants ) )
			{
				flagComponent.Owner = TeamFlag;
			}

			GameObject.Destroy();
		}
	}

	[Authority]
	public void ResetPos()
	{
		var spawnPoint = Scene.GetAll<FlagSpawn>()?.FirstOrDefault( x => x.Team == TeamFlag );

		if ( spawnPoint.IsValid() )
		{
			Transform.ClearInterpolation();

			WorldTransform = spawnPoint.WorldTransform;
		}
	}
}

[Title( "CTF Trigger" )]
public sealed class CTFTrigger : Component, Component.ITriggerListener
{
	[Header( "Should be the opposite team of the side its on" )]
	[Property, Sync] public Team Team { get; set; }

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

			local.TeamComponent?.ResetToSpawnPoint();

			//gs.AddFlagCapture( local.TeamComponent.Team );

			PopupHolder.BroadcastPopup( $"{local.TeamComponent.Team} captured the flag!", 5 );
		}
	}
}

public sealed class FlagSpawn : Component
{
	[Property] public Team Team { get; set; }

	protected override void DrawGizmos()
	{
		Model model = Model.Load( "models/editor/spawnpoint.vmdl" );
		Gizmo.Hitbox.Model( model );
		Gizmo.Draw.Color = TeamSpawnPoint.GetTeamColor( Team ).WithAlpha( (Gizmo.IsHovered || Gizmo.IsSelected) ? 0.7f : 0.5f );
		SceneObject sceneObject = Gizmo.Draw.Model( model );
		if ( sceneObject != null )
		{
			sceneObject.Flags.CastShadows = true;
		}
	}
}
