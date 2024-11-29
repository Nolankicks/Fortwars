public sealed class WallComponent : Component, Component.ExecuteInEditor
{
	protected override void OnEnabled()
	{
		if ( GameObject.NetworkMode != NetworkMode.Object )
		{
			GameObject.NetworkMode = NetworkMode.Object;
			Log.Info( "NetworkMode set to Object" );
		}
	}

	[Rpc.Broadcast]
	public void ToggleEnable( bool enable )
	{
		if ( Scene.IsEditor )
			return;

		foreach ( var c in Components.GetAll().ToList() )
		{
			if ( c is WallComponent )
				continue;

			c.Enabled = enable;
			Network.Refresh();
		}
	}

	[ActionGraphNode( "Toggle Wall" ), Rpc.Broadcast]
	public static void ToggleWall( bool enable )
	{
		foreach ( var wall in Game.ActiveScene?.GetAll<WallComponent>() )
		{
			wall.ToggleEnable( enable );
		}
	}
}
