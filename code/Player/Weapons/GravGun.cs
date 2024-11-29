using Sandbox.Events;

public class Gravgun : Item, IGameEventHandler<DeathEvent>
{
	bool CanPickup = false;
	bool CouldPickup = false;
	TimeSince timeSinceLastCanPickup = 10f;

	protected virtual float MaxPullDistance => 2000f;
	protected virtual float MaxPushDistance => 500;
	protected virtual float LinearFrequency => 10f;
	protected virtual float LinearDampingRatio => 1f;
	protected virtual float AngularFrequency => 10f;
	protected virtual float AngularDampingRatio => 1f;
	protected virtual float PullForce => 20f;
	protected virtual float PushForce => 1000f;
	protected virtual float ThrowForce => 2000f;
	protected virtual float HoldDistance => 50f;
	protected virtual float DropCooldown => 0.5f;
	protected virtual float BreakLinearForce => 2000f;

	public const string GrabbedTag = "grabbed";

	public Vector3 HeldPosition { get; private set; }
	public Rotation HeldRotation { get; private set; }
	public Vector3 HoldPosition { get; private set; }
	public Rotation HoldRotation { get; private set; }

	[Sync] public int GrabbedBone { get; set; }
	PhysicsBody HeldBody = null;
	[Sync] public GameObject GrabbedObject { get; set; }

	SceneTrace GravGunTrace => Scene.Trace.Ray( new Ray( FWPlayerController.Local.Eye.WorldPosition, FWPlayerController.Local.EyeAngles.Forward ), 375f )
			.IgnoreGameObjectHierarchy( GameObject.Root )
			.WithoutTags( FW.Tags.Trigger, FW.Tags.Player )
			.WithTag( FW.Tags.Rollermine );


	[Property] ParticleEmitter GravGunParticles { get; set; }

	protected override void OnFixedUpdate()
	{
		if ( IsProxy ) return;

		var tr = GravGunTrace.Run();

		if ( GrabbedObject.IsValid() )
		{
			CanPickup = false;
		}
		else
		{
			CanPickup = tr.Body.IsValid() && tr.Body.BodyType == PhysicsBodyType.Dynamic && tr.GameObject.Components.TryGet<RollerMine>( out var p )
			&& !tr.GameObject.Root.Tags.HasAny( FW.Tags.Player, FW.Tags.Map );
			if ( CanPickup ) timeSinceLastCanPickup = 0f;
		}

		if ( CanPickup && !CouldPickup )
		{
			CouldPickup = true;
		}
		else if ( CouldPickup && !CanPickup )
		{
			if ( timeSinceLastCanPickup > 1f )
			{
				CouldPickup = false;
			}
		}

		var player = FWPlayerController.Local;

		if ( !player.IsValid() )
			return;

		if ( Input.Pressed( "attack1" ) )
		{
			PrimaryUse();

			GameObject.Dispatch( new WeaponAnimEvent( "b_attack", true ) );
		}
		else if ( Input.Pressed( "attack2" ) )
		{
			SecondaryUse();

			GameObject.Dispatch( new WeaponAnimEvent( "b_attack", true ) );
		}

		// Actually moving the prop
		GrabMove( player.Eye.WorldPosition, player.EyeAngles.Forward, player.Eye.WorldRotation );
		PhysicsStep();

		// Visuals
		GravGunParticles.Enabled = GrabbedObject.IsValid();
	}

	protected override void OnDisabled()
	{
		GrabEnd();
	}

	void PrimaryUse()
	{
		if ( GrabbedObject.IsValid() )
		{
			GrabEnd();
			CanPickup = true;
		}

		if ( CanPickup )
		{
			var tr = GravGunTrace.Run();

			if ( tr.GameObject?.Components?.TryGet<MapInstance>( out var mapInstance, FindMode.EverythingInSelfAndParent ) ?? false )
				return;

			if ( tr.Body.IsValid() )
			{
				if ( tr.Body.BodyType == PhysicsBodyType.Static )
					tr.Body.BodyType = PhysicsBodyType.Dynamic;

				tr.GameObject.Network.SetOwnerTransfer( OwnerTransfer.Takeover );

				tr.GameObject.Network.TakeOwnership();
				tr.Body.Velocity += FWPlayerController.Local.EyeAngles.Forward * ThrowForce;
			}
		}
		else
		{
			timeSinceLastCanPickup = 10f;
		}
	}

	void SecondaryUse()
	{
		if ( !GrabbedObject.IsValid() )
		{
			var player = FWPlayerController.Local;

			if ( !player.IsValid() )
				return;

			var tr = GravGunTrace.Run();

			if ( !tr.Hit )
				return;

			//if ( !tr.GameObject.Components.TryGet<MapInstance>( out var m, FindMode.EverythingInSelfAndParent ) )
			//	return;

			if ( tr.GameObject.Tags.Has( FW.Tags.Map ) && !tr.GameObject.Tags.Has( FW.Tags.Rollermine ) )
				return;

			if ( tr.Body.IsValid() )
			{
				GrabInit( tr.GameObject, tr.Body, player.Eye.WorldPosition + player.Eye.WorldRotation.Forward * HoldDistance, player.EyeAngles );
			}
		}
		else
		{
			if ( GrabbedObject.IsValid() )
			{
				GrabEnd();
			}
			else
			{
				timeSinceLastCanPickup = 10f;
			}
		}
	}

	void PhysicsStep()
	{
		if ( !HeldBody.IsValid() ) return;

		var heldObject = HeldBody.GetGameObject();

		//if ( heldObject.IsValid() && heldObject.Network.Owner != GameObject.Network.Owner )
		//{
		//	GrabEnd( false );
		//	Log.Info( "Owner mismatch" );

		//	return;
		//}

		var velocity = HeldBody.Velocity;
		Vector3.SmoothDamp( HeldBody.Position, HoldPosition, ref velocity, 0.075f, Time.Delta );
		HeldBody.Velocity = velocity;

		var angularVelocity = HeldBody.AngularVelocity;
		Rotation.SmoothDamp( HeldBody.Rotation, HoldRotation, ref angularVelocity, 0.075f, Time.Delta );
		HeldBody.AngularVelocity = angularVelocity;

		var heldBodyGb = HeldBody.GetGameObject();

		var local = FWPlayerController.Local;

		var gs = Scene.GetAll<GameSystem>()?.FirstOrDefault();

		if ( !heldBodyGb.IsValid() || !local.IsValid() || !gs.IsValid() )
			return;

		if ( !heldBodyGb.Tags.Has( FW.Tags.Rollermine ) )
			return;

		var teamComponent = local.TeamComponent;

		if ( !teamComponent.IsValid() )
			return;

		var team = teamComponent.Team;

		if ( heldBodyGb.Network.Owner != local.Network.Owner )
		{
			GrabEnd( false );
			Log.Info( "Owner mismatch" );

			return;
		}

		gs.SubtractTimeHeld( team, Time.Delta );
	}

	void GrabInit( GameObject gameObject, PhysicsBody body, Vector3 grabPosition, Rotation grabRotation )
	{
		GrabbedObject = gameObject;

		gameObject.Network.SetOwnerTransfer( OwnerTransfer.Takeover );
		gameObject.Network.TakeOwnership();
		GrabbedBone = body.GroupIndex;

		if ( body.BodyType == PhysicsBodyType.Static )
			body.BodyType = PhysicsBodyType.Dynamic;

		Physgun.AddTag( gameObject, GrabbedTag );

		HeldBody = body;
		HeldPosition = HeldBody.LocalMassCenter;
		HeldRotation = grabRotation.Inverse * HeldBody.Rotation;

		HoldPosition = HeldBody.Position;
		HoldRotation = HeldBody.Rotation;

		HeldBody.Sleeping = false;
		HeldBody.AutoSleep = false;

		var local = FWPlayerController.Local;

		if ( gameObject.Components.TryGet<FortwarsProp>( out var prop ) && local.IsValid() )
			prop.SetGrabber( local );

		if ( gameObject.Components.TryGet<RollerMine>( out var rollerMine, FindMode.EverythingInSelfAndParent ) )
			rollerMine.SetGrabbed( this, true );

		GameObject.Dispatch<WeaponAnimEvent>( new( "b_empty", true ) );
	}

	void IGameEventHandler<DeathEvent>.OnGameEvent( DeathEvent eventArgs )
	{
		if ( !GrabbedObject.IsValid() )
			return;

		GrabEnd();
	}

	void GrabMove( Vector3 startPos, Vector3 dir, Rotation rot )
	{
		if ( HeldBody.IsValid() )
		{
			var attachPos = HeldBody.FindClosestPoint( startPos );
			var holdDistance = HoldDistance + attachPos.Distance( HeldBody.MassCenter );

			HoldPosition = startPos - HeldPosition * HeldBody.Rotation + dir * holdDistance;
			HoldRotation = rot * HeldRotation;
		}
	}

	[Rpc.Owner]
	public void GrabEnd( bool setGrabber = true )
	{
		if ( !GrabbedObject.IsValid() ) return;

		if ( setGrabber )
		{
			if ( GrabbedObject.Components.TryGet<RollerMine>( out var rollerMine, FindMode.EverythingInSelfAndParent ) )
				rollerMine.SetGrabbed( null, false );

			if ( GrabbedObject.Components.TryGet<FortwarsProp>( out var prop ) )
				prop.SetGrabber( null );

			Physgun.RemoveTag( GrabbedObject, GrabbedTag );
		}

		GrabbedObject = null;
		HeldBody = null;

		GameObject.Dispatch<WeaponAnimEvent>( new( "b_empty", false ) );
	}
}
