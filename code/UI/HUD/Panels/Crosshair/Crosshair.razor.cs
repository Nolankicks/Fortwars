using Sandbox.UI;
using System;

public partial class Crosshair : Panel
{
	public bool ShowCrosshair { get; set; } = true;

	private Material CrosshairMat { get; set; }

	private RenderAttributes attributes { get; set; }

	protected override void OnAfterTreeRender( bool firstTime )
	{
		base.OnAfterTreeRender( firstTime );
		attributes = new RenderAttributes();
		attributes.Set( "Texture", Texture.Load( "textures/Crosshairs/crosshair008.vtex" ) );

	}

	protected override int BuildHash()
	{
		var hash = new HashCode();
		hash.Add( Time.Now );
		return hash.ToHashCode();
	}

	public override void DrawBackground( ref RenderState state )
	{
		base.DrawBackground( ref state );

		var pos = new Vector2( 0, 0 );
		var center = Screen.Size / 2;

		Graphics.DrawQuad( Box.RectOuter, Material.UI.Basic, Color.White, attributes );
	}
}
