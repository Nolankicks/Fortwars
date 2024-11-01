using Sandbox.UI;
using System;
public partial class AmmoPanel : Panel
{
	Item weapon;

	public override void Tick()
	{
		var inv = FWPlayerController.Local.Inventory;
		if ( inv.CurrentItem.IsValid() )
		{
			weapon = inv.CurrentItem.Components.Get<Item>();
		}
	}

	protected override int BuildHash()
	{
		var hash = new HashCode();
		var inv = FWPlayerController.Local.Inventory;
		hash.Add( weapon );
		if ( inv.CurrentItem.Components.TryGet<Item>( out var item ) )
		{
			hash.Add( item.Ammo );
			hash.Add( item.UsesAmmo );
		}
		return hash.ToHashCode();
	}
}
