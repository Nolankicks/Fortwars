public sealed class NavMarker : Component
{
	[Property] public Color Tint { get; set; }
	[Property] public string Text { get; set; }

	[Property] public bool DrawDistance { get; set; } = false;

	protected override void DrawGizmos()
	{
		Gizmo.Draw.Color = Tint;
		Gizmo.Transform = Scene.Transform.World;
		Gizmo.Draw.Sprite( WorldPosition, 12.0f, "textures/ui/navmarker.png" );

		// Lets only draw the text while close
		if ( WorldPosition.Distance( Gizmo.Camera.Position ) > 250 )
			return;
		Gizmo.Draw.Text( Text, Transform.World );
	}

	public float GetDistance()
	{
		if ( !Scene.Camera.IsValid() )
			return 0;
		return Scene.Camera.WorldPosition.Distance( WorldPosition );
	}
}
