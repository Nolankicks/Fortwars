using Sandbox.Citizen;
using Sandbox.Events;

public partial class CaptureTheFlag
{
	public void OnActive( Connection connection )
	{
		connection.CanRefreshObjects = true;

		if ( !GameSystem.PlayerPrefab.IsValid() || !GameSystem.SpawnPlayer )
			return;

		var spawns = Scene.GetAllComponents<TeamSpawnPoint>().Where( x => x.Team == Team.None ).ToList();
		Transform SpawnTransform = spawns.Count > 0 ? Game.Random.FromList( spawns ).Transform.World : Transform.World;

		var player = GameSystem.PlayerPrefab.Clone( SpawnTransform );

		if ( player.Components.TryGet<FWPlayerController>( out var p ) )
			p.SetWorld( SpawnTransform );

		if ( player.Components.TryGet<CitizenAnimationHelper>( out var animHelper, FindMode.EnabledInSelfAndChildren ) && animHelper.Target.IsValid() )
		{
			var clothing = new ClothingContainer();
			clothing.Deserialize( connection.GetUserData( "avatar" ) );
			clothing.Apply( animHelper.Target );
		}

		player.NetworkSpawn( connection );

		//Handle joining mid game
		if ( GameSystem.State != GameSystem.GameState.Waiting && GameSystem.State != GameSystem.GameState.Ended )
		{
			if ( player.Components.TryGet<FWPlayerController>( out var playerController ) )
			{
				var teams = GameSystem.FourTeams ? new List<Team> { Team.Red, Team.Blue, Team.Yellow, Team.Green } : new List<Team> { Team.Red, Team.Blue };

				var inv = playerController.Inventory;

				if ( !inv.IsValid() )
					return;

				if ( !GameSystem.FourTeams )
				{
					var redPlayers = Scene.GetAllComponents<TeamComponent>().Where( x => x.Team == Team.Red ).Count();
					var bluePlayers = Scene.GetAllComponents<TeamComponent>().Where( x => x.Team == Team.Blue ).Count();
					if ( redPlayers > bluePlayers )
						playerController.TeamComponent?.SetTeam( Team.Blue );
					else
						playerController.TeamComponent?.SetTeam( Team.Red );

				}
				else
				{
					playerController.TeamComponent?.SetTeam( Game.Random.FromList( teams ) );
				}

				playerController.TeleportToTeamSpawnPoint();

				if ( GameSystem.State == GameSystem.GameState.BuildMode || GameSystem.State == GameSystem.GameState.OvertimeBuild )
				{
					//inv.OpenClassSelect();
					inv.AddItem( ResourceLibrary.GetAll<WeaponData>().FirstOrDefault( x => x.ResourceName == "propgun" ) );
					//inv.AddItem( ResourceLibrary.GetAll<WeaponData>().FirstOrDefault( x => x.ResourceName == "physgun" ) );
				}
				else if ( GameSystem.State == GameSystem.GameState.FightMode || GameSystem.State == GameSystem.GameState.OvertimeFight )
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
