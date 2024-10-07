using System;
using Sandbox;
using Sandbox.Utility.Svg;

public sealed class PlayerTrigger : Component, Component.ITriggerListener
{
	public delegate void TriggerAction( PlayerController Player );

	[Property] public TriggerAction OnEnter { get; set; }
	[Property] public TriggerAction OnExit { get; set; }

	public void OnTriggerEnter( Collider other )
	{
		if ( other.GameObject.Components.TryGet<PlayerController>( out var player, FindMode.EverythingInSelfAndParent ) )
		{
			OnEnter?.Invoke( player );
		}
	}

	public void OnTriggerExit( Collider other )
	{
		if ( other.GameObject.Components.TryGet<PlayerController>( out var player, FindMode.EverythingInSelfAndParent ) )
		{
			OnExit?.Invoke( player );
		}
	}
}
