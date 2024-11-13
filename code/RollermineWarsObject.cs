using Sandbox;
using Sandbox.Events;

public sealed class RollermineWarsObject : HealthComponent
{
	[Property] public Team Team { get; set; }

	public override void OnDeath( GameObject Attacker, Vector3 damagePos, Vector3 damageNormal )
	{
		var gamemode = Scene?.GetAll<GameMode>()?.FirstOrDefault();

		if ( gamemode.IsValid() )
			gamemode.EndGame( Team );
	}
}
