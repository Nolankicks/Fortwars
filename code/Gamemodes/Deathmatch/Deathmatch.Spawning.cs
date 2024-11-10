using Sandbox;
using Sandbox.Citizen;

public partial class Deathmatch
{
	public override void OnActive( Connection connection )
	{
		if ( !GameSystem.PlayerPrefab.IsValid() || !GameSystem.SpawnPlayer )
			return;

		var player = GameSystem.PlayerPrefab.Clone();

		if ( player.Components.TryGet<CitizenAnimationHelper>( out var animHelper, FindMode.EnabledInSelfAndChildren ) && animHelper.Target.IsValid() )
		{
			var clothing = new ClothingContainer();
			clothing.Deserialize( connection.GetUserData( "avatar" ) );
			clothing.Apply( animHelper.Target );
		}

		if ( player.Components.TryGet<FWPlayerController>( out var playerController ) )
		{
			playerController.TeleportToAnySpawnPoint();
		}

		player.NetworkSpawn( connection );
	}
}
