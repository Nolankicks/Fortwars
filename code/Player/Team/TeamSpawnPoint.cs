using Sandbox;

[EditorHandle("materials/gizmo/spawnpoint.png")]
public sealed class TeamSpawnPoint : Component
{
	[Property] public Team Team { get; set; } = Team.None;

	protected override void DrawGizmos()
	{
		Model model = Model.Load( "models/editor/spawnpoint.vmdl" );
		Gizmo.Hitbox.Model( model );
		Gizmo.Draw.Color = GetTeamColor( Team ).WithAlpha( (Gizmo.IsHovered || Gizmo.IsSelected) ? 0.7f : 0.5f );
		SceneObject sceneObject = Gizmo.Draw.Model( model );
		if ( sceneObject != null )
		{
			sceneObject.Flags.CastShadows = true;
		}
	}

	public static Color GetTeamColor( Team team )
	{
		return team switch
		{
			Team.Blue => Color.Cyan,
			Team.Red => Color.Red,
			Team.Yellow => Color.Yellow,
			Team.Green => Color.Green,
			_ => Color.White
		};
	}
}
