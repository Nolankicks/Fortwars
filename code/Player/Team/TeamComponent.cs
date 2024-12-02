using Sandbox.Audio;

public enum Team
{
	Red,
	Blue,
	Yellow,
	Green,
	None
}

public sealed class TeamComponent : Component
{
	[Property, Sync] public Team Team { get; set; } = Team.None;
	[Property, Sync] public FWPlayerController Player { get; set; }

	[Rpc.Broadcast]
	public void SetTeam( Team team, bool resetToSpawnPoint = false )
	{
		Team = team;

		var controller = Components.Get<FWPlayerController>();
		var nameTag = Components.Get<NameTag>( FindMode.EverythingInSelfAndChildren );

		if ( controller.IsValid() )
		{
			foreach ( var renderer in controller?.AnimHelper?.Target.Components.GetAll<SkinnedModelRenderer>() )
			{
				Color originalColor = renderer.Tint;
				Color teamColor = team switch
				{
					Team.Red => Color.Red,
					Team.Blue => BlendColors( Color.Blue, Color.Cyan, 0.65f ),
					Team.Yellow => Color.Yellow,
					Team.Green => Color.Green,
					_ => Color.White
				};

				renderer.Tint = BlendColors( originalColor, teamColor, 0.6f );
			}

			// HACK
			foreach ( var col in Team.GetNames( typeof( Team ) ) )
			{
				if ( col != team.ToString() )
					continue;

				controller.Tags.Add( col.ToLower() );
				controller?.shrimpleCharacterController?.IgnoreTags?.Add( col.ToLower() );
			}
		}

		if ( nameTag.IsValid() )
		{
			nameTag.SetTeam( team );
		}

		if ( resetToSpawnPoint )
			ResetToSpawnPoint();
	}

	private Color BlendColors( Color original, Color tint, float blendFactor )
	{
		return new Color(
			original.r * (1 - blendFactor) + tint.r * blendFactor,
			original.g * (1 - blendFactor) + tint.g * blendFactor,
			original.b * (1 - blendFactor) + tint.b * blendFactor,
			original.a
		);
	}

	public bool IsFriendly( TeamComponent other )
	{
		if ( Team == Team.None || other.Team == Team.None )
			return true;

		return Team == other.Team;
	}

	[Rpc.Owner]
	public void ResetToSpawnPoint()
	{
		var spawns = Scene.GetAll<TeamSpawnPoint>()?.Where( x => x.Team == Team )?.ToList();

		if ( Team == Team.None )
			spawns.AddRange( spawns );

		if ( spawns == null || spawns.Count == 0 )
			return;

		var spawn = Game.Random.FromList( spawns );

		if ( !spawn.IsValid() )
			return;

		if ( GameObject.Components.TryGet<FWPlayerController>( out var player ) )
		{
			player.SetWorld( spawn.Transform.World );
		}
	}

	[Rpc.Owner]
	public void PlayFlagCapturedSound( Team capturingTeam )
	{
		if ( Team != capturingTeam )
		{
			var handle = Sound.Play( "audio/objectives/flag.captured.sound" );

			if ( handle.IsValid() )
				handle.TargetMixer = Mixer.FindMixerByName( "ui" );
		}
		else if ( Team != Team.None )
		{
			var handle = Sound.Play( "audio/enemy.flag.captured.sound" );

			if ( handle.IsValid() )
				handle.TargetMixer = Mixer.FindMixerByName( "ui" );
		}
	}

	[ActionGraphNode( "Reset All Teams" )]
	public static void TeleportAllTeams()
	{
		Game.ActiveScene?.GetAll<TeamComponent>()?.ToList()?.ForEach( x => x.ResetToSpawnPoint() );
	}

	public static Team GetTeamLowestCount()
	{
		var blueCount = Game.ActiveScene.GetAllComponents<FWPlayerController>().Where( x => x.TeamComponent.Team == Team.Blue ).Count();
		var redCount = Game.ActiveScene.GetAllComponents<FWPlayerController>().Where( x => x.TeamComponent.Team == Team.Red ).Count();

		if ( blueCount < redCount )
			return Team.Blue;
		else
			return Team.Red;
	}

	public static void ResetTeams()
	{
		foreach ( var team in Game.ActiveScene.GetAllComponents<TeamComponent>() )
		{
			team.Team = Team.None;
		}
	}
}
