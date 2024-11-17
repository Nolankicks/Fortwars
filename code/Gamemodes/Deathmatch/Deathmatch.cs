public sealed partial class Deathmatch : GameMode
{
	public override void WinGame( Team team = Team.None )
	{
		Scene.GetAll<Inventory>()?.ToList()?.ForEach( x =>
		{
			x.ClearAll();
			x.ClearSelectedClass();
		} );

		Scene.GetAll<HealthComponent>()?.ToList()?.ForEach( x => x.ResetHealth() );

		var player = WinningPlayer();

		if ( player.IsValid() )
		{
			PopupHolder.BroadcastPopup( $"{player.Network.Owner.DisplayName} won", 5 );
		}

		GameSystem.GameState = GameSystem.GameStates.S_END;
	}

	public FWPlayerController WinningPlayer()
	{
		var players = Scene.GetAll<FWPlayerController>();
		var highest = players.OrderBy( x => x.Kills ).FirstOrDefault();

		return highest;
	}
}
