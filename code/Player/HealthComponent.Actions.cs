using Sandbox;

public partial class HealthComponent
{
	[ActionGraphNode( "Is Friendly" ), Pure]
	public static bool IsFriendly( GameObject Attacker, Team team, bool ifNull = true )
	{
		if ( !Attacker.IsValid() )
			return ifNull;

		if ( Attacker.Components.TryGet<TeamComponent>( out var teamComponent ) )
		{
			return teamComponent.Team == team;
		}

		return ifNull;
	}
}
