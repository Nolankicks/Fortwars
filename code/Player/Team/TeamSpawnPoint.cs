using Sandbox;

public sealed class TeamSpawnPoint : Component
{
	[Property] public Team Team { get; set; } = Team.None;

	protected override void DrawGizmos()
	{
		Model model = Model.Load( "models/editor/spawnpoint.vmdl" );
		Gizmo.Hitbox.Model( model );
		Gizmo.Draw.Color = GetTeamColor().WithAlpha( (Gizmo.IsHovered || Gizmo.IsSelected) ? 0.7f : 0.5f );
		SceneObject sceneObject = Gizmo.Draw.Model( model );
		if ( sceneObject != null )
		{
			sceneObject.Flags.CastShadows = true;
		}
	}

	public Color GetTeamColor()
	{
		return Team switch
		{
			Team.Blue => Color.Cyan,
			Team.Red => Color.Red,
			Team.Yellow => Color.Yellow,
			Team.Green => Color.Green,
			_ => Color.White
		};
	}
}