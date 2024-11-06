public class TeamManager : Component
{
	[Property] public Team Team { get; set; }
	[Property, Sync] public int Budget { get; set; } = 1000;

	public static int MaxBudget { get; set; } = 1000;

	[Sync] public NetDictionary<PropResource, int> PropCounts { get; set; } = new NetDictionary<PropResource, int>();

	public static TeamManager GetManager( Team team )
	{
		return Game.ActiveScene.GetAllComponents<TeamManager>().Where( x => x.Team == team ).FirstOrDefault();
	}

	protected override void OnEnabled()
	{
		if ( IsProxy )
			return;

		foreach ( var prop in ResourceLibrary.GetAll<PropResource>().Where( x => !x.Hidden ) )
		{
			PropCounts.Append( new( prop, 10 ) );
		}
	}
}
