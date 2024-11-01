using Sandbox.UI;
using System;
public partial class AmmoPanel : Panel
{
	Item weapon;

	public override void Tick()
	{
        var local = FWPlayerController.Local;

        if ( !local.IsValid() )
            return;

		var inv = local.Inventory;

        if ( !inv.IsValid() )
            return;
        
		if ( inv.CurrentItem.IsValid() )
		{
			weapon = inv.CurrentItem.Components.Get<Item>();
		}
	}

	protected override int BuildHash()
	{
        return HashCode.Combine( weapon?.Ammo, weapon?.UsesAmmo );
	}
}
