using Sandbox;
using Sandbox.Events;

public sealed class RollerMine : Component, IGameEventHandler<OnGameEnd>, IGameEventHandler<OnBuildMode>,
IGameEventHandler<OnFightMode>, IGameEventHandler<OnGameOvertimeBuild>, IGameEventHandler<OnGameOvertimeFight>,
IGameEventHandler<OnGameWaiting>
{
	[Sync] public Transform StartingTransform { get; set; }

	protected override void OnStart()
	{
		if ( Networking.IsHost )
			StartingTransform = Transform.World;
	}

	[Authority]
	public void ResetPosition()
	{
		if ( Components.TryGet<Rigidbody>( out var rb ) )
		{
			rb.Velocity = Vector3.Zero;
			rb.AngularVelocity = Vector3.Zero;
		}

		Transform.ClearInterpolation();

		Transform.World = StartingTransform;
	}

	void IGameEventHandler<OnGameEnd>.OnGameEvent( OnGameEnd eventArgs )
	{
		DestroyGb();
	}

	void IGameEventHandler<OnBuildMode>.OnGameEvent( OnBuildMode eventArgs )
	{
		DestroyGb();
	}

	void IGameEventHandler<OnFightMode>.OnGameEvent( OnFightMode eventArgs )
	{
		DestroyGb();
	}

	void IGameEventHandler<OnGameOvertimeBuild>.OnGameEvent( OnGameOvertimeBuild eventArgs )
	{
		DestroyGb();
	}

	void IGameEventHandler<OnGameOvertimeFight>.OnGameEvent( OnGameOvertimeFight eventArgs )
	{
		DestroyGb();
	}

	void IGameEventHandler<OnGameWaiting>.OnGameEvent( OnGameWaiting eventArgs )
	{
		DestroyGb();
	}

	[Broadcast]
	public void DestroyGb()
	{
		GameObject?.Destroy();
	}
}
