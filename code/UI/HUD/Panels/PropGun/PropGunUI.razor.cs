using Sandbox.UI;
using System;


public partial class PropGunUI : Panel
{
	public string GetMode()
	{
		var pg = GetPropGun();
		if ( pg.IsValid() )
		{
			return pg.GetModeString();
		}
		return "";
	}

	public string GetItemName()
	{
		var pg = GetPropGun();
		if ( pg.IsValid() )
		{
			return pg.CurrentProp?.DisplayName;
		}
		return "";
	}

	public Propgun GetPropGun()
	{
		var player = FWPlayerController.Local;
		if ( !player.IsValid() )
			return null;

		var inventory = player.Inventory;
		if ( !inventory.IsValid() || !inventory.CurrentItem.IsValid() )
			return null;

		return inventory.CurrentItem.Components.Get<Propgun>();
	}

	protected override int BuildHash()
	{
		var hash = new HashCode();
		hash.Add( GetMode() );
		hash.Add( GetPropGun() );
		hash.Add( GetItemName() );
		hash.Add( GetPropGun()?.SnapToGrid );
		return hash.ToHashCode();
	}
}
