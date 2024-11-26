using Sandbox;

[Title( "CFT Trigger" )]
public sealed class CFTTrigger : Component, Component.ITriggerListener
{
	void ITriggerListener.OnTriggerEnter( Collider other )
	{
		var flag = ResourceLibrary.Get<WeaponData>( "weapondatas/flag.weapons" );

		var local = FWPlayerController.Local;

		if ( !local.IsValid() )
			return;

		var gs = GameSystem.Instance;

		if ( !gs.IsValid() || flag is null || !local.TeamComponent.IsValid() )
			return;

		if ( local.Inventory.IsValid() && local.Inventory.ItemsData.Contains( flag ) )
		{
			gs.AddFlagCapture( local.TeamComponent.Team );
		}
	}
}
