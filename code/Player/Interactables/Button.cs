using System;
using Sandbox;

public sealed class Button : Component, Component.IPressable
{
	public delegate void PressAction( GameObject Presser );

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
		OnPress?.Invoke( e.Source.GameObject );
		OnPressRPC( e.Source.GameObject );

		return true;
	}

	void IPressable.Release( IPressable.Event e )
	{
		OnRelease?.Invoke( e.Source?.GameObject );
		OnReleaseRPC( e.Source?.GameObject );
	}

	[Broadcast]
	public void OnPressRPC( GameObject presser )
	{
		OnPressBroadcast?.Invoke( presser );
	}

	[Broadcast]
	public void OnReleaseRPC( GameObject presser )
	{
		OnReleaseBroadcast?.Invoke( presser );
	}
}
