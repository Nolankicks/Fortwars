public sealed class RollerMine : Component
{
	[Sync] public Transform StartingTransform { get; set; }
	[Sync] public bool IsGrabbed { get; set; } = false;
	[Property, Sync] public SkinnedModelRenderer Renderer { get; set; }
	[Property, Sync] public Gravgun Grabber { get; set; }

	[Property] NavMarker Marker { get; set; }

	protected override void OnStart()
	{
		if ( Networking.IsHost )
		{
			StartingTransform = Transform.World;
			
			Network.SetOwnerTransfer( OwnerTransfer.Takeover );
			Network.SetOrphanedMode( NetworkOrphaned.ClearOwner );
		}
	}

	[Rpc.Owner]
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

	[Rpc.Broadcast]
	public void SetGrabbed( Gravgun gravgun, bool isGrabbed )
	{
		IsGrabbed = isGrabbed;
		Grabber = gravgun;
	}

	protected override void OnFixedUpdate()
	{
		if ( !Marker.IsValid() )
			return;

		if ( Grabber.IsValid() )
		{
			Marker.Enabled = Grabber.IsProxy;
		}
		else
			Marker.Enabled = true;

		// Handle color
		if ( Grabber.IsValid() )
		{
			var team = Scene.GetAllComponents<FWPlayerController>().Where( x => x.Network.Owner == Grabber.Network.Owner ).First().Components.Get<TeamComponent>();

			if ( team.IsValid() )
			{
				Marker.Tint = HUD.GetColor( team.Team ).WithAlpha( 0.8f );
			}
			else
				Marker.Tint = Color.White.WithAlpha( 0.8f );
		}
		else
			Marker.Tint = Color.White.WithAlpha( 0.8f );
	}
}
