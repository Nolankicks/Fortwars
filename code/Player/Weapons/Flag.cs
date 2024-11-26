using Sandbox;
using Sandbox.Events;

public sealed class Flag : Item
{
	[Property] public GameObject DroppedFlagPrefab { get; set; }

	[Property, Sync] public Team Owner { get; set; }

	protected override void OnDisabled()
	{
		base.OnDisabled();

		if ( IsProxy )
			return;

		DropFlag();
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

	public void DropFlag()
	{
		var local = FWPlayerController.Local;

		if ( !local.IsValid() || (!local?.Inventory.IsValid() ?? true) )
			return;

		if ( local.Inventory.CurrentItem == GameObject )
			Log.Info( "Flag disabled" );

		var clone = DroppedFlagPrefab.Clone( WorldPosition + local.EyeAngles.Forward * 100 );

		if ( clone.Components.TryGet<DroppedFlag>( out var droppedFlag ) )
		{
			droppedFlag.TeamFlag = Owner;
		}

		clone.NetworkSpawn( null );

		local.Inventory.RemoveItem( GameObject, false );
	}
}

public sealed class DroppedFlag : Component, Component.ITriggerListener
{
	[Property] public Team TeamFlag { get; set; }

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
			{
				Log.Info( "Can't pick up flag because of team mismatch or invalid inventory" );
				return;
			}

			inv.DisableAll();

			if ( flag is not null )
				inv.AddItem( flag, enabled: true, changeIndex: true );

			if ( inv.Components.TryGet<Flag>( out var flagComponent, FindMode.EverythingInSelfAndChildren ) )
			{
				flagComponent.Owner = TeamFlag;
			}

			GameObject.Destroy();
		}
	}
}
