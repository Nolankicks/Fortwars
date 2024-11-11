using Sandbox;
using Sandbox.Events;

public sealed class RollerMine : Component
{
	[Sync] public Transform StartingTransform { get; set; }
	[Sync] public bool IsGrabbed { get; set; } = false;
	[Property, Sync] public SkinnedModelRenderer Renderer { get; set; }
	[Property, Sync] public Gravgun Grabber { get; set; }

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

		Grabber?.GrabEnd();

		Transform.ClearInterpolation();

		Transform.World = StartingTransform;
	}

	protected override void OnUpdate()
	{
		Renderer?.Set( "b_open", IsGrabbed );
	}

	[ActionGraphNode( "Reset Rollermine" )]
	public static void ResetRollermine()
	{
		foreach ( var rollerMine in Game.ActiveScene?.GetAll<RollerMine>() )
		{
			rollerMine.ResetPosition();
		}
	}

	[Broadcast]
	public void SetGrabbed( Gravgun gravgun, bool isGrabbed )
	{
		IsGrabbed = isGrabbed;
		Grabber = gravgun;
	}
}
