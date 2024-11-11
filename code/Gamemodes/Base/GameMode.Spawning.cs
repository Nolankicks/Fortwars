using Sandbox.Citizen;
using Sandbox.Events;

public partial class GameMode
{
	public virtual void OnActive( Connection connection )
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

		//We need a better way of joining mid game. But I'm not sure what the best way is. Maybe an action on the round gameobject?

		//Event callback
		Scene.Dispatch( new OnPlayerJoin() );
	}


}
