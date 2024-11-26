using Sandbox;

public sealed class CaptureTheFlag : GameMode
{
	public override void CheckForWinningTeam()
	{
		var teams = new Dictionary<Team, int>
		{
			{ Team.Red, GameSystem.RedFlagsCaptured },
			{ Team.Blue, GameSystem.BlueFlagsCaptured }
		};

		var max = teams.Max( x => x.Value );

		if ( teams.Any( x => x.Value >= 3 ) )
		{
			EndGame( teams.FirstOrDefault( x => x.Value == max ).Key );
		}
	}

	public override Team WinningTeam()
	{
		var teams = new Dictionary<Team, int>
		{
			{ Team.Red, GameSystem.RedFlagsCaptured },
			{ Team.Blue, GameSystem.BlueFlagsCaptured }
		};

		var max = teams.Max( x => x.Value );

		if ( max >= 3 )
		{
			return teams.FirstOrDefault( x => x.Value == max ).Key;
		}

		return Team.None;
	}
}
