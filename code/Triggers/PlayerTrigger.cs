using System;
using Sandbox;
using Sandbox.Utility.Svg;

public sealed class PlayerTrigger : Component, Component.ITriggerListener
{
	public delegate void TriggerAction( FWPlayerController Player );

	[Property] public TriggerAction OnEnter { get; set; }
	[Property] public TriggerAction OnExit { get; set; }

	[Property] public TriggerAction OnEnterBroadcast { get; set; }
	[Property] public TriggerAction OnExitBroadcast { get; set; }

	public void OnTriggerEnter( Collider other )
	{
		if ( other.GameObject.Components.TryGet<FWPlayerController>( out var player, FindMode.EverythingInSelfAndParent ) )
		{
			OnEnter?.Invoke( player );
			OnEnterRPC( player );
		}
	}

	public void OnTriggerExit( Collider other )
	{
		if ( other.GameObject.Components.TryGet<FWPlayerController>( out var player, FindMode.EverythingInSelfAndParent ) )
		{
			OnExit?.Invoke( player );
			OnExitRPC( player );
		}
	}

	[Rpc.Broadcast]
	public void OnEnterRPC( FWPlayerController player )
	{
		OnEnterBroadcast?.Invoke( player );
	}

	[Rpc.Broadcast]
	public void OnExitRPC( FWPlayerController player )
	{
		OnExitBroadcast?.Invoke( player );
	}
}
