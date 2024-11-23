using Sandbox;

public sealed class DodgeBall : GameMode
{
	public override Team WinningTeam()
	{
		var blue = Scene.GetAll<FWPlayerController>().Where( x => !x.HealthComponent.IsDead && x.TeamComponent.Team == Team.Blue );
		var red = Scene.GetAll<FWPlayerController>().Where( x => !x.HealthComponent.IsDead && x.TeamComponent.Team == Team.Red );

		if ( blue is null || red is null )
			return Team.None;

		if ( !blue.Any() && !red.Any() )
			return Team.None;

		if ( !blue.Any() )
			return Team.Red;

		if ( !red.Any() )
			return Team.Blue;

		return Team.None;
	}

	public override void CheckForWinningTeam()
	{
		var blue = Scene.GetAll<FWPlayerController>().Where( x => !x.HealthComponent.IsDead && x.TeamComponent.Team == Team.Blue );
		var red = Scene.GetAll<FWPlayerController>().Where( x => !x.HealthComponent.IsDead && x.TeamComponent.Team == Team.Red );

		if ( blue is null || red is null )
			return;

		if ( !blue.Any() && !red.Any() )
			return;

		if ( !blue.Any() )
		{
			EndGame( Team.Red );
			return;
		}

		if ( !red.Any() )
		{
			EndGame( Team.Blue );
			return;
		}
	}
}
