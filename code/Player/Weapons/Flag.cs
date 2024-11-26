using Sandbox;
using Sandbox.Events;

public sealed class Flag : Item
{
	[Property] public GameObject DroppedFlagPrefab { get; set; }

	protected override void OnDisabled()
	{
		base.OnDisabled();

		if ( IsProxy )
			return;

		var local = FWPlayerController.Local;

		if ( !local.IsValid() || (!local?.Inventory.IsValid() ?? true) )
			return;

		if ( local.Inventory.CurrentItem == GameObject )
			Log.Info( "Flag disabled" );

		var clone = DroppedFlagPrefab.Clone( WorldPosition + local.EyeAngles.Forward * 100 );

		clone.NetworkSpawn( null );

		local.Inventory.RemoveItem( GameObject, false );
	}
}

public sealed class DroppedFlag : Component, Component.ITriggerListener
{
	void ITriggerListener.OnTriggerEnter( Collider other )
	{
		if ( IsProxy )
			return;

		if ( other.GameObject.Components.TryGet<Inventory>( out var inv, FindMode.EverythingInSelfAndParent ) )
		{
			var flag = ResourceLibrary.Get<WeaponData>( "weapondatas/flag.weapons" );

			inv.DisableAll();

			if ( flag is not null )
				inv.AddItem( flag, enabled: true, changeIndex: true );

			GameObject.Destroy();
		}
	}
}
