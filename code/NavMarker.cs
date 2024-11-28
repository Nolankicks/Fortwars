public sealed class NavMarker : Component
{
	[Property] public Color Tint { get; set; } = Color.White.WithAlpha( 1.0f );
	[Property] public string Text { get; set; }

	protected override void OnUpdate()
	{
		base.OnUpdate();
		Gizmo.Draw.LineSphere( WorldPosition, 4.0f );
	}

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
}
