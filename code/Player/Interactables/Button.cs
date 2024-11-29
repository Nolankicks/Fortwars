using System;
using Sandbox;

public sealed class Button : Component, Component.IPressable
{
	public delegate void PressAction( GameObject Presser, FWPlayerController PresserPlayer, HealthComponent PresserHealth );

	[Property] public PressAction OnPress { get; set; }
	[Property] public PressAction OnPressBroadcast { get; set; }
	[Property] public PressAction OnRelease { get; set; }
	[Property] public PressAction OnReleaseBroadcast { get; set; }
	[Property] public Func<bool> CanPress { get; set; }

	bool IPressable.CanPress( IPressable.Event e )
	{
		return CanPress?.Invoke() ?? true;
	}

	bool IPressable.Press( IPressable.Event e )
	{
		var player = e.Source.GameObject.Components.Get<FWPlayerController>();
		var health = e.Source.GameObject.Components.Get<HealthComponent>();

		if ( !player.IsValid() || !health.IsValid() )
			return false;

		OnPress?.Invoke( e.Source.GameObject, player, health );
		OnPressRPC( e.Source.GameObject );

		return true;
	}

	void IPressable.Release( IPressable.Event e )
	{
		var player = e.Source.GameObject.Components.Get<FWPlayerController>();
		var health = e.Source.GameObject.Components.Get<HealthComponent>();

		if ( !player.IsValid() || !health.IsValid() )
			return;

		OnRelease?.Invoke( e.Source?.GameObject, player, health );
		OnReleaseRPC( e.Source?.GameObject );
	}

	[Rpc.Broadcast]
	public void OnPressRPC( GameObject presser )
	{
		var player = presser.Components.Get<FWPlayerController>();
		var health = presser.Components.Get<HealthComponent>();

		if ( !player.IsValid() || !health.IsValid() )
			return;

		OnPressBroadcast?.Invoke( presser, player, health );
	}

	[Rpc.Broadcast]
	public void OnReleaseRPC( GameObject presser )
	{
		var player = presser.Components.Get<FWPlayerController>();
		var health = presser.Components.Get<HealthComponent>();

		if ( !player.IsValid() || !health.IsValid() )
			return;

		OnReleaseBroadcast?.Invoke( presser, player, health );
	}
}
