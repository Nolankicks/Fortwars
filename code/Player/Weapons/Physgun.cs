
using System;
using Sandbox.Events;

public class Physgun : Item
{
	public PhysicsBody HeldBody { get; private set; }
	public Vector3 HeldPosition { get; private set; }
	public Rotation HeldRotation { get; private set; }
	public Vector3 HoldPosition { get; private set; }
	public Rotation HoldRotation { get; private set; }
	public float HoldDistance { get; private set; }
	public bool Grabbing { get; private set; }

	float MinTargetDistance => 0f;
	float MaxTargetDistance => 10_000f;
	float LinearFrequency => 20f;
	float LinearDampingRatio => 1.0f;
	float AngularFrequency => 20f;
	float AngularDampingRatio => 1.0f;
	float TargetDistanceSpeed => 25.0f;
	float RotateSpeed => 0.25f;
	float RotateSnapAt => 45f;

	[Sync] public int GrabbedBone { get; set; }
	[Sync] public Vector3 GrabbedPosition { get; set; }

	[Sync] public GameObject GrabbedObject { get; set; }

	protected override void OnUpdate()
	{
		if ( IsProxy )
			return;

		var Player = PlayerController.Local;

		if ( !Player.IsValid() ) return;

		var eyePos = Player.Eye.WorldPosition;
		var eyeDir = Player.EyeAngles.Forward;
		var eyeRot = Rotation.From( 0, Player.EyeAngles.yaw, 0 );

		if ( Input.Pressed( "attack1" ) )
		{
			if ( !Grabbing ) Grabbing = true;
		}

		bool grabEnabled = Grabbing && Input.Down( "attack1" );
		bool wantsToFreeze = Input.Pressed( "attack2" );

		if ( grabEnabled )
		{
			if ( HeldBody.IsValid() )
			{
				UpdateGrab( eyePos, eyeRot, eyeDir, wantsToFreeze );
			}
			else
			{
				TryStartGrab( eyePos, eyeRot, eyeDir );
			}
		}
		else if ( Grabbing )
		{
			GrabEnd();
		}

		if ( !Grabbing && Input.Pressed( "reload" ) )
		{
			TryUnfreeze();
		}

		PhysicsStep();
	}

	private void TryUnfreeze()
	{
		if ( !Scene.Camera.IsValid() )
			return;

		var tr = Scene.Trace.Ray( Scene.Camera.ScreenNormalToRay( 0.5f ), 1000 )
			.UseHitboxes()
			.IgnoreGameObjectHierarchy( GameObject.Root )
			.WithoutTags( FW.Tags.Player, FW.Tags.Trigger, FW.Tags.Map )
			.Run();

		if ( !tr.Hit || !tr.Body.IsValid() )
			return;

		var go = tr.GameObject;
		if ( go.Tags.Has( FW.Tags.Map ) || go.Parent.Tags.Has( FW.Tags.Map ) )
			return;

		Log.Info( $"Unfreezing {go}" );

		var physicsGroup = tr.Body.PhysicsGroup;

		if ( tr.Body.IsValid() )
			tr.Body.BodyType = PhysicsBodyType.Dynamic;

		if ( physicsGroup == null )
			return;

		go.Network.SetOwnerTransfer( OwnerTransfer.Takeover );
		go.Root.Network.TakeOwnership();

		Log.Info( $"Unfreezing {physicsGroup.BodyCount} bodies" );

		for ( int i = 0; i < physicsGroup.BodyCount; i++ )
		{
			var body = physicsGroup.GetBody( i );

			if ( body.BodyType != PhysicsBodyType.Dynamic )
			{
				body.BodyType = PhysicsBodyType.Dynamic;
			}
		}
	}

	private void TryStartGrab( Vector3 eyePos, Rotation eyeRot, Vector3 eyeDir )
	{
		var tr = Scene.Trace.Ray( eyePos, eyePos + eyeDir * MaxTargetDistance )
			.UseHitboxes()
			.IgnoreGameObjectHierarchy( GameObject.Root )
			.WithoutTags( FW.Tags.Player, FW.Tags.Trigger )
			.Run();

		if ( !tr.Hit || !tr.GameObject.IsValid() || tr.StartedSolid )
			return;

		var rootObject = tr.GameObject.Root;

		if ( !tr.GameObject.Components.TryGet<Rigidbody>( out var rigidbody, FindMode.EverythingInSelfAndAncestors ) )
			return;

		var body = rigidbody.PhysicsBody;

		// Don't move keyframed objects and ignore the Wall
		if ( body.BodyType == PhysicsBodyType.Keyframed || tr.GameObject.Components.TryGet<MapCollider>( out _, FindMode.EverythingInSelfAndAncestors ) )
			return;

		// Unfreeze
		if ( body.BodyType != PhysicsBodyType.Dynamic )
		{
			body.BodyType = PhysicsBodyType.Dynamic;
			body.Sleeping = false;
		}

		GrabInit( body, eyePos, tr.EndPosition, eyeRot );

		GrabbedObject = tr.GameObject;

		tr.GameObject.Network.SetOwnerTransfer( OwnerTransfer.Takeover );
		tr.GameObject.Network.TakeOwnership();

		AddTag( tr.GameObject, "grabbed" );

		GrabbedPosition = body.Transform.PointToLocal( tr.EndPosition );
		GrabbedBone = body.GroupIndex;
	}

	[Broadcast]
	public static void AddTag( GameObject obj, string tag )
	{
		if ( obj.IsValid() )
		{
			obj.Tags.Add( tag );
		}
	}

	[Broadcast]
	public static void RemoveTag( GameObject obj, string tag )
	{
		if ( obj.IsValid() )
		{
			obj.Tags.Remove( tag );
		}
	}

	void UpdateGrab( Vector3 eyePos, Rotation eyeRot, Vector3 eyeDir, bool wantsToFreeze )
	{
		var Player = PlayerController.Local;

		if ( !Player.IsValid() ) return;

		if ( wantsToFreeze )
		{
			if ( HeldBody.BodyType == PhysicsBodyType.Dynamic )
			{
				HeldBody.BodyType = PhysicsBodyType.Static;
				HeldBody.Velocity = 0;
				HeldBody.AngularVelocity = 0;
			}

			GrabEnd();
			return;
		}

		MoveTargetDistance( Input.MouseWheel.y * TargetDistanceSpeed );

		bool rotating = Input.Down( "use" );
		bool snapping = false;

		if ( rotating )
		{
			DoRotate( eyeRot, Input.MouseDelta * RotateSpeed );
			snapping = Input.Down( "run" );
			Player.CanMoveHead = false;
		}

		GrabMove( eyePos, eyeDir, eyeRot, snapping );
	}

	void GrabInit( PhysicsBody body, Vector3 startPosition, Vector3 grabPosition, Rotation rot )
	{
		if ( !body.IsValid() ) return;

		GrabEnd();

		Grabbing = true;
		HeldBody = body;
		HoldDistance = Vector3.DistanceBetween( startPosition, grabPosition );
		HoldDistance = HoldDistance.Clamp( MinTargetDistance, MaxTargetDistance );

		HeldRotation = rot.Inverse * HeldBody.Rotation;
		HeldPosition = HeldBody.Transform.PointToLocal( grabPosition );

		HoldPosition = HeldBody.Position;
		HoldRotation = HeldBody.Rotation;

		HeldBody.Sleeping = false;
		HeldBody.AutoSleep = false;

		Scene.Dispatch( new OnPhysgunGrabChange( true ) );
	}

	private void GrabEnd()
	{
		if ( HeldBody.IsValid() )
		{
			HeldBody.AutoSleep = true;

			if ( HeldBody.BodyType == PhysicsBodyType.Dynamic )
			{
				HeldBody.BodyType = PhysicsBodyType.Static;
				HeldBody.Velocity = 0;
				HeldBody.AngularVelocity = 0;
			}
		}

		HeldBody = null;
		Grabbing = false;

		RemoveTag( GrabbedObject, "grabbed" );

		GrabbedObject = null;
		Scene.Dispatch( new OnPhysgunGrabChange( false ) );
	}

	void PhysicsStep()
	{
		if ( !HeldBody.IsValid() ) return;

		var velocity = HeldBody.Velocity;
		Vector3.SmoothDamp( HeldBody.Position, HoldPosition, ref velocity, 0.075f, Time.Delta );
		HeldBody.Velocity = velocity;

		var angularVelocity = HeldBody.AngularVelocity;
		Rotation.SmoothDamp( HeldBody.Rotation, HoldRotation, ref angularVelocity, 0.075f, Time.Delta );
		HeldBody.AngularVelocity = angularVelocity;
	}

	void GrabMove( Vector3 startPosition, Vector3 dir, Rotation rot, bool snapAngles )
	{
		if ( !HeldBody.IsValid() ) return;

		HoldPosition = startPosition - HeldPosition * HeldBody.Rotation + dir * HoldDistance;

		HoldRotation = rot * HeldRotation;

		if ( snapAngles )
		{
			var angles = HoldRotation.Angles();
			HoldRotation = Rotation.From(
				MathF.Round( angles.pitch / RotateSnapAt ) * RotateSnapAt,
				MathF.Round( angles.yaw / RotateSnapAt ) * RotateSnapAt,
				MathF.Round( angles.roll / RotateSnapAt ) * RotateSnapAt
			);
		}
	}

	private void MoveTargetDistance( float distance )
	{
		HoldDistance += distance;
		HoldDistance = HoldDistance.Clamp( MinTargetDistance, MaxTargetDistance );
	}

	protected virtual void DoRotate( Rotation eye, Vector3 input )
	{
		bool rotating = Input.Down( "use" );
		bool snapping = false;
		var player = PlayerController.Local;

		if ( !player.IsValid() )
			return;

		if ( rotating )
		{
			snapping = Input.Down( "run" );
			player.CanMoveHead = false;
		}
		var localRot = eye;
		localRot *= Rotation.FromAxis( Vector3.Up, input.x * RotateSpeed );
		localRot *= Rotation.FromAxis( Vector3.Right, input.y * RotateSpeed );
		localRot = eye.Inverse * localRot;

		HeldRotation = localRot * HeldRotation;
	}
}
