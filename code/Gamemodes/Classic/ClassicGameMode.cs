using System;

public partial class ClassicGameMode : GameMode
{
	//I think this might be a good idea to keep? We would be defining this in code anyway
	public override void CheckForWinningTeam()
	{
		var teams = new Dictionary<Team, float>
	{
		{ Team.Red, GameSystem.RedTimeHeld },
		{ Team.Blue, GameSystem.BlueTimeHeld },
		{ Team.Yellow, GameSystem.YellowTimeHeld },
		{ Team.Green, GameSystem.GreenTimeHeld }
	};

		var max = teams.Min( x => x.Value );
		var winningTeam = teams.FirstOrDefault( x => x.Value == max ).Key;

		if ( teams.Any( x => x.Value <= 0 ) )
		{
			EndGame( winningTeam );
		}
	}

	public override Team WinningTeam()
	{
		var teams = new Dictionary<Team, float>
		{
			{ Team.Red, GameSystem.RedTimeHeld },
			{ Team.Blue, GameSystem.BlueTimeHeld },
			{ Team.Yellow, GameSystem.YellowTimeHeld },
			{ Team.Green, GameSystem.GreenTimeHeld }
		};

		var min = teams.Min( x => x.Value );

		if ( Math.Round( min, 1 ) <= 0 )
		{
			return teams.FirstOrDefault( x => x.Value == min ).Key;
		}

		return Team.None;
	}
}
