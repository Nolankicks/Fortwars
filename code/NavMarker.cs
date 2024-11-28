public sealed class NavMarker : Component
{
	[Property] public Color Tint { get; set; } = Color.White.WithAlpha( 1.0f );
	[Property] public string Text { get; set; }

	protected override void OnUpdate()
	{
		base.OnUpdate();
		Gizmo.Draw.LineSphere( WorldPosition, 4.0f );
	}
}
