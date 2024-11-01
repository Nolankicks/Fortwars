using Sandbox.Events;

public enum Team
{
	Red,
	Blue,
	Yellow,
	Green,
	None
}

public sealed class TeamComponent : Component, IGameEventHandler<OnBuildMode>,
	IGameEventHandler<OnFightMode>, IGameEventHandler<OnGameOvertimeBuild>, IGameEventHandler<OnGameOvertimeFight>
{
	[Property, Sync] public Team Team { get; set; } = Team.None;

	[Broadcast]
	public void SetTeam( Team team )
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

	void IGameEventHandler<OnBuildMode>.OnGameEvent( OnBuildMode eventArgs )
	{
		ResetToSpawnPoint();
	}

	void IGameEventHandler<OnFightMode>.OnGameEvent( OnFightMode eventArgs )
	{
		ResetToSpawnPoint();
	}

	void IGameEventHandler<OnGameOvertimeBuild>.OnGameEvent( OnGameOvertimeBuild eventArgs )
	{
		ResetToSpawnPoint();
	}

	void IGameEventHandler<OnGameOvertimeFight>.OnGameEvent( OnGameOvertimeFight eventArgs )
	{
		ResetToSpawnPoint();
	}


	[Authority]
	public void ResetToSpawnPoint()
	{
		if ( Team == Team.None )
			return;

		var spawns = Scene.GetAll<TeamSpawnPoint>()?.Where( x => x.Team == Team )?.ToList();

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
}
