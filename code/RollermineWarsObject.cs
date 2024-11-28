public sealed class RollermineWarsObject : HealthComponent
{
	[Property] public Team Team { get; set; }

	public override void OnDeath( GameObject Attacker, Vector3 damagePos, Vector3 damageNormal )
	{
		var gamemode = Scene?.GetAll<GameMode>()?.FirstOrDefault();

		Log.Info( $"RollermineWarsObject: {GameObject} has died" );

		if ( gamemode.IsValid() )
			gamemode.EndGame( Team );
	}

	public override void TakeDamage( GameObject Attacker, int damage = 10, Vector3 HitPos = default, Vector3 normal = default, bool spawnFlag = true, int boneId = 0 )
	{
		if ( Attacker.Components.TryGet<TeamComponent>( out var team, FindMode.EverythingInSelfAndParent ) && team.Team != Team )
		{
			Log.Info( "RollermineWarsObject: Can't damage your own team" );
			return;
		}

		base.TakeDamage( Attacker, damage, HitPos, normal );
	}
}
