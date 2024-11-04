using Sandbox.Citizen;
using Sandbox.Events;
using System;

public record OnItemEquipped() : IGameEvent;

public sealed class Inventory : Component
{
	[Property, Sync, Category( "Lists" )] public List<GameObject> Items { get; set; } = new();
	[Property, Sync, Category( "Lists" )] public List<WeaponData> ItemsData { get; set; } = new();
	[Property, Sync, Category( "Current Item" )] public int Index { get; set; } = 0;
	[Property, Sync, Category( "Current Item" )] public GameObject CurrentItem { get; set; }
	[Property, Sync, Category( "Current Item" )] public WeaponData CurrentWeaponData { get; set; }
	[Sync] public PlayerClass SelectedClass { get; set; }
	public bool CanScrollSwitch { get; set; } = true;

	[Button, Category( "Buttons" )]
	public void SwapItemsButton()
	{
		SwapItems( 2, 1 );

		var hud = Scene.GetAll<HUD>()?.FirstOrDefault();

		if ( hud.IsValid() )
			hud.StateHasChanged();
	}

	[Authority]
	public void SwapItems( int index1, int index2 )
	{
		if ( Items?.Count() == 0 || Items is null )
			return;

		if ( index1 < 0 || index1 >= Items.Count() || index2 < 0 || index2 >= Items.Count() )
		{
			Log.Warning( "Invalid index" );
			return;
		}

		var temp = Items[index1];
		Items[index1] = Items[index2];
		Items[index2] = temp;

		var tempData = ItemsData[index1];
		ItemsData[index1] = ItemsData[index2];
		ItemsData[index2] = tempData;

		if ( Index == index1 )
			Index = index2;
		else if ( Index == index2 )
			Index = index1;
	}

	[Description( "Swaps the items in the inventory, please null check the GameObjects before calling this method." )]
	public void SwapItems( GameObject gb1, GameObject gb2 )
	{
		if ( !gb1.IsValid() || !gb2.IsValid() )
			return;

		var index1 = Items.IndexOf( gb1 );
		var index2 = Items.IndexOf( gb2 );

		if ( index1 == -1 || index2 == -1 )
			return;

		SwapItems( index1, index2 );
	}

	protected override void OnUpdate()
	{
		if ( IsProxy )
			return;

		if ( Input.Pressed( "view" ) )
		{
			var hud = Scene.GetAll<HUD>()?.FirstOrDefault();

			if ( !hud.IsValid() )
				return;

			if ( hud.Panel.ChildrenOfType<ClassSelect>().Count() != 0 )
				return;

			var gs = GameSystem.Instance;
			var local = FWPlayerController.Local;

			if ( !gs.IsValid() || !local.IsValid() )
				return;

			if ( gs.State != GameSystem.GameState.Waiting && gs.State != GameSystem.GameState.Ended )
			{
				OpenClassSelect();
			}
		}

		if ( Items?.Count() == 0 || Items is null )
			return;

		if ( Input.MouseWheel.y != 0 && CanScrollSwitch )
		{
			NextWeapon( Math.Sign( Input.MouseWheel.y ) );
		}

		/*if ( Input.Pressed( "Slot1" ) )
		{
			NextWeapon( 1 );
		}*/

		if ( Index < 0 )
		{
			Index = Items.Count() - 1;
			ChangeItem( Index, Items );
		}

		KeyboardInputs();
	}

	[Authority]
	public void ClearAll()
	{
		var items = Items?.ToList();

		if ( items is null )
			return;

		foreach ( var item in items )
		{
			RemoveItem( item );
		}

		Items.Clear();
		ItemsData.Clear();
	}

	[Button, Category( "Buttons" )]
	public void AddItemButton()
	{
		AddItem( ItemsData[0] );
	}

	void NextWeapon( int dir )
	{
		var local = FWPlayerController.Local;
		if ( !local.IsValid() || (!local?.CanMoveHead ?? false) )
			return;

		Index = (Index - dir) % Items.Count();
		ChangeItem( Index, Items );
	}

	[Authority]
	public void AddItem( WeaponData item, bool insert = false, int spot = 0 )
	{
		if ( IsProxy || item is null )
			return;

		if ( !item.WeaponPrefab.IsValid() )
		{
			Log.Warning( "Invalid weapon prefab" );
			return;
		}

		var clone = item.WeaponPrefab.Clone();

		var local = FWPlayerController.Local;

		if ( !clone.IsValid() || !local.IsValid() || (!local?.Eye.IsValid() ?? false) )
			return;

		clone.Parent = local.Eye;

		clone.Enabled = false;

		clone.NetworkSpawn( false, Network.Owner );

		if ( !insert )
		{
			Items.Add( clone );
			ItemsData.Add( item );
		}
		else
		{
			Items.Insert( spot, clone );
			ItemsData.Insert( spot, item );
		}

		if ( Items.Count() == 1 )
		{
			ChangeItem( 0, Items );
		}
	}

	[Authority]
	public void AddItemAt( WeaponData item, int index )
	{
		if ( item is null || index < 0 || index >= Items.Count() || IsProxy )
			return;

		if ( !item.WeaponPrefab.IsValid() )
		{
			Log.Warning( "Invalid weapon prefab" );
			return;
		}

		RemoveItem( index );

		var clone = item.WeaponPrefab.Clone();

		var local = FWPlayerController.Local;

		if ( !clone.IsValid() || !local.IsValid() || (!local?.Eye.IsValid() ?? false) )
			return;

		clone.Parent = local.Eye;

		clone.Enabled = false;

		clone.NetworkSpawn( false, Network.Owner );

		Items.Insert( index, clone );
		ItemsData.Insert( index, item );
	}

	[Button, Category( "Buttons" )]
	public void AddItemAtButton()
	{
		AddItemAt( ItemsData[1], 0 );
	}

	[Button, Category( "Buttons" )]
	public void RemoveItemButton()
	{
		RemoveItem( Items[0] );
	}

	[Authority]
	public void RemoveItem( GameObject item )
	{
		if ( !item.IsValid() || Items is null || ItemsData is null )
			return;

		var dataIndex = Items.IndexOf( item );

		if ( dataIndex < 0 || dataIndex >= ItemsData.Count() )
			return;

		ItemsData.RemoveAt( Items.IndexOf( item ) );

		if ( Items.Contains( item ) )
			Items.Remove( item );

		item.Destroy();

		if ( CurrentItem == item )
		{
			ChangeItem( 0, Items );
		}

		var player = FWPlayerController.Local;

		if ( !player.IsValid() )
			return;

		if ( Items.Count() == 0 )
		{
			CurrentItem = null;
			CurrentWeaponData = null;

			player.HoldType = CitizenAnimationHelper.HoldTypes.None;

			if ( player.HoldRenderer.IsValid() )
				FWPlayerController.ClearHoldRenderer( player.HoldRenderer );
		}
	}

	[Authority]
	public void RemoveItem( int index )
	{
		if ( index < 0 || index >= Items.Count() )
			return;

		RemoveItem( Items[index] );
	}

	[Authority]
	public void RemoveItem( WeaponData item )
	{
		if ( ItemsData is null || item is null )
			return;

		RemoveItem( ItemsData.IndexOf( item ) );
	}

	[Authority]
	public void ChangeItem( int index, List<GameObject> items )
	{
		if ( index < 0 || index >= items?.Count() || items is null || items.ElementAt( index ) == CurrentItem )
			return;

		Index = index;

		if ( CurrentItem.IsValid() )
			CurrentItem.Enabled = false;

		var nextItem = items?[index];

		if ( !nextItem.IsValid() )
			return;

		CurrentItem = nextItem;

		CurrentWeaponData = ItemsData[index];

		if ( !CurrentItem.IsValid() )
			return;

		CurrentItem.Enabled = true;

		CurrentItem.Dispatch( new OnItemEquipped() );
	}

	[Button, Authority, Category( "Buttons" )]
	public void DisableAll()
	{
		foreach ( var item in Items )
		{
			if ( item.IsValid() )
				item.Enabled = false;
		}

		CurrentItem = null;

		CurrentWeaponData = null;
	}

	[Authority]
	public void ClearSelectedClass()
	{
		var local = FWPlayerController.Local;

		if ( local.IsValid() )
		{
			local.ResetMaxPlayerHealth();
			local.ResetSpeed();
		}

		SelectedClass = null;
	}

	public void KeyboardInputs()
	{
		if ( Input.Pressed( "slot1" ) )
		{
			ChangeItem( 0, Items );
		}

		if ( Input.Pressed( "slot2" ) )
		{
			ChangeItem( 1, Items );
		}

		if ( Input.Pressed( "slot3" ) )
		{
			ChangeItem( 2, Items );
		}

		if ( Input.Pressed( "slot4" ) )
		{
			ChangeItem( 3, Items );
		}

		if ( Input.Pressed( "slot5" ) )
		{
			ChangeItem( 4, Items );
		}

		if ( Input.Pressed( "slot6" ) )
		{
			ChangeItem( 5, Items );
		}

		if ( Input.Pressed( "slot7" ) )
		{
			ChangeItem( 6, Items );
		}

		if ( Input.Pressed( "slot8" ) )
		{
			ChangeItem( 7, Items );
		}

		if ( Input.Pressed( "slot9" ) )
		{
			ChangeItem( 8, Items );
		}
	}

	[Authority]
	public void OpenClassSelect()
	{
		if ( IsProxy )
			return;

		var hud = Scene.GetAll<HUD>()?.FirstOrDefault();

		if ( !hud.IsValid() )
			return;

		var classSelect = new ClassSelect();

		classSelect.Inventory = this;

		hud.Panel.AddChild( classSelect );

		Log.Info( "Opened class select" );
	}

	[Authority]
	public void AddClass( PlayerClass playerClass )
	{
		Log.Info( "Adding class" );

		var gs = Scene.GetAll<GameSystem>()?.FirstOrDefault();

		if ( !gs.IsValid() )
			return;

		if ( playerClass is null )
			return;

		if ( SelectedClass is not null && (gs.State == GameSystem.GameState.FightMode || gs.State == GameSystem.GameState.OvertimeFight) )
		{
			ClearAll();

			AddItem( ResourceLibrary.GetAll<WeaponData>().FirstOrDefault( x => x.ResourceName == "gravgun" ), true, 0 );

			ChangeItem( 0, Items );
		}

		SelectedClass = playerClass;

		var local = FWPlayerController.Local;

		if ( local.IsValid() && playerClass is not null )
		{
			local.SetSpeed( playerClass.WalkSpeed, playerClass.RunSpeed );
			local.SetPlayerMaxHealth( playerClass.Health );
		}

		if ( gs.State == GameSystem.GameState.FightMode || gs.State == GameSystem.GameState.OvertimeFight )
		{
			AddItem( playerClass.WeaponData, true, 0 );

			ChangeItem( 0, Items );

			Log.Info( "Added weapon" );
		}
	}
	[Authority]
	public void ResetAmmo()
	{
		var local = FWPlayerController.Local;

		if ( !local.IsValid() )
			return;

		foreach ( var item in Items )
		{
			if ( item.IsValid() )
			{
				var weapon = item.Components.Get<Item>( true );

				if ( !weapon.IsValid() )
					continue;

				if ( weapon.IsValid() )
				{
					weapon.Reload();
				}
			}
		}
	}

	public WeaponData GetOtherWeapon()
	{
		if ( Items.Count() < 2 )
			return null;

		int index = Items.IndexOf( CurrentItem ) == 0 ? 1 : 0;

		return ItemsData[index];
	}
}
