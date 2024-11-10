using Sandbox;

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
	}
}
