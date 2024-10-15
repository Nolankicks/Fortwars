
using Sandbox.Citizen;
using Sandbox.Events;

public partial class GameSystem
{
	public void OnActive( Connection connection )
	{
		//Don't spawn the player if we are the dedicated server
		if ( Application.IsHeadless && Connection.Local == connection )
			return;

		connection.CanRefreshObjects = true;

		if ( !PlayerPrefab.IsValid() || !SpawnPlayer )
			return;

		var spawns = Scene.GetAllComponents<TeamSpawnPoint>().ToList();
		Transform SpawnTransform = spawns.Count > 0 ? Game.Random.FromList( spawns ).Transform.World : Transform.World;

		var player = PlayerPrefab.Clone( SpawnTransform );

		if ( player.Components.TryGet<PlayerController>( out var p ) )
			p.SetWorld( SpawnTransform );

		if ( player.Components.TryGet<CitizenAnimationHelper>( out var animHelper, FindMode.EnabledInSelfAndChildren ) && animHelper.Target.IsValid() )
		{
			var clothing = new ClothingContainer();
			clothing.Deserialize( connection.GetUserData( "avatar" ) );
			clothing.Apply( animHelper.Target );
		}

		player.NetworkSpawn( connection );

        //Handel joining mid game
		if ( State != GameState.Waiting && State != GameState.Ended )
		{
			if ( player.Components.TryGet<PlayerController>( out var playerController ) )
			{
				var teams = FourTeams ? new List<Team> { Team.Red, Team.Blue, Team.Yellow, Team.Green } : new List<Team> { Team.Red, Team.Blue };

				var inv = playerController.Inventory;

				if ( !inv.IsValid() )
					return;

				playerController.TeamComponent?.SetTeam( Game.Random.FromList( teams ) );

				playerController.TeleportToTeamSpawnPoint();

				if ( State == GameState.BuildMode || State == GameState.OvertimeBuild )
				{
					//inv.OpenClassSelect();
					inv.AddItem( ResourceLibrary.GetAll<WeaponData>().FirstOrDefault( x => x.ResourceName == "propgun" ) );
					inv.AddItem( ResourceLibrary.GetAll<WeaponData>().FirstOrDefault( x => x.ResourceName == "physgun" ) );
				}
				else if ( State == GameState.FightMode || State == GameState.OvertimeFight )
				{
					inv.OpenClassSelect();

					playerController.Inventory.AddItem( ResourceLibrary.GetAll<WeaponData>().FirstOrDefault( x => x.ResourceName == "gravgun" ) );

					if ( inv.SelectedClass is not null )
						inv.AddItem( inv.SelectedClass.WeaponData );
				}
			}
		}

        //Event callback
		Scene.Dispatch( new OnPlayerJoin() );
	}
}
