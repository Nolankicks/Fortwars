using System;
using Sandbox;

public sealed class SpawnWallTrigger : Component, Component.ITriggerListener
{
	void ITriggerListener.OnTriggerEnter( Collider other )
	{
		if ( other.GameObject.Components.TryGet<RollerMine>( out var rollerMine, FindMode.EverythingInSelfAndParent ) )
		{
			rollerMine.ResetPosition();
			Log.Info( "GrabEnd" );
		}
	}
}
