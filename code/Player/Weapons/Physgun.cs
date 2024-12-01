
using Sandbox.Events;
using System;

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

	public bool MouseInput { get; set; } = false;

	public PhysicsBody MouseBody { get; set; }
	public float DistanceBetweenMouseObject { get; set; }
	public Rotation StartingBodyRotation { get; set; }

	[Property, Category( "Effects" )] LineRenderer PhysLine { get; set; }
	[Property, Category( "Effects" )] GameObject EndLineObject { get; set; }

	[Property, Category( "Effects" )] ParticleEffect LineParticles { get; set; }

	[Property, Category( "Effects" )] ParticleBoxEmitter Emitter { get; set; }

	protected override void OnUpdate()
	{
		base.OnUpdate();

		if ( IsProxy )
			return;

		var Player = FWPlayerController.Local;

		if ( !Player.IsValid() ) return;

		/*if ( Input.Pressed( "mouseprop" ) )
		{
			MouseInput = !MouseInput;

			Mouse.Visible = MouseInput;
		}*/

		// Rotate the viewmodel around
		if ( MouseInput )
		{
			WorldRotation = Rotation.Lerp( WorldRotation, Rotation.LookAt( Scene.Camera.ScreenPixelToRay( Mouse.Position ).Forward ), Time.Delta * 20.0f );
		}
		else
		{
			LocalRotation = Rotation.Identity;
		}

		if ( MouseInput )
		{
			var tr = Scene.Trace.Ray( Scene.Camera.ScreenPixelToRay( Mouse.Position ), 1000 )
				.IgnoreGameObjectHierarchy( GameObject.Root )
				.WithoutTags( FW.Tags.Player, FW.Tags.Trigger, FW.Tags.Map, "held" )
				.Run();

			/*if ( Input.Pressed( "attack1" ) )
			{
				Log.Info( "Pressed attack1" );
				if ( tr.Body.IsValid() )
				{
					var go = tr.Body.GetGameObject();
					StartingBodyRotation = tr.Body.Rotation;

					if ( go.IsValid() )
					{
						go.Network.TakeOwnership();

						AddTag( go, "held" );
					}

					DistanceBetweenMouseObject = tr.Distance;

					MouseBody = tr.Body;

					go.Components.Get<FortwarsProp>().Rigidbody.Enabled = true;
				}
			}

			if ( Input.Released( "attack1" ) )
			{
				if ( MouseBody.IsValid() )
				{
					MouseBody.GetGameObject().Components.Get<FortwarsProp>().Rigidbody.Enabled = false;

					var go = MouseBody.GetGameObject();

					if ( go.IsValid() )
					{
						go.Network.DropOwnership();

						RemoveTag( go, "held" );
					}
				}

				MouseBody = null;
			}

			if ( MouseBody.IsValid() )
			{
				var GrabTrace = Scene.Trace.Ray( Scene.Camera.ScreenPixelToRay( Mouse.Position ), DistanceBetweenMouseObject )
					.WithoutTags( "held", FW.Tags.Player, FW.Tags.Trigger, FW.Tags.Map )
					.Run();

				Log.Info( "MouseBody is valid" );

				MouseBody.SmoothMove( GrabTrace.EndPosition, 0.5f, Time.Delta );
				MouseBody.Rotation = StartingBodyRotation;
			}*/

			return;
		}

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

		PhysicsStep();

		UpdateEffects();
	}

	private void TryStartGrab( Vector3 eyePos, Rotation eyeRot, Vector3 eyeDir )
	{
		var tr = Scene.Trace.Ray( eyePos, eyePos + eyeDir * MaxTargetDistance )
			.UseHitboxes()
			.IgnoreGameObjectHierarchy( GameObject.Root )
			.WithoutTags( FW.Tags.Player, FW.Tags.Trigger, FW.Tags.Rollermine, "flag" )
			.Run();

		if ( !tr.Hit || !tr.GameObject.IsValid() || tr.StartedSolid )
			return;

		var rootObject = tr.GameObject.Root;

		if ( tr.GameObject.Components.TryGet<FortwarsProp>( out var p, FindMode.EverythingInSelfAndParent ) )
		{
			p.Rigidbody.Enabled = true;
		}

		if ( !tr.GameObject.Components.TryGet<Rigidbody>( out var rigidbody, FindMode.EverythingInSelfAndAncestors ) )
			return;

		var body = rigidbody.PhysicsBody;

		// Don't move keyframed objects and ignore the Wall
		if ( body.BodyType == PhysicsBodyType.Keyframed || tr.GameObject.Components.TryGet<MapCollider>( out _, FindMode.EverythingInSelfAndAncestors ) )
			return;

		GrabInit( body, eyePos, tr.EndPosition, eyeRot );

		GrabbedObject = tr.GameObject;

		tr.GameObject.Network.SetOwnerTransfer( OwnerTransfer.Takeover );
		tr.GameObject.Network.TakeOwnership();

		AddTag( tr.GameObject, "grabbed" );

		GrabbedPosition = body.Transform.PointToLocal( tr.EndPosition );
		GrabbedBone = body.GroupIndex;
	}

	[Rpc.Broadcast]
	public static void AddTag( GameObject obj, string tag )
	{
		if ( obj.IsValid() )
		{
			obj.Tags.Add( tag );
		}
	}

	[Rpc.Broadcast]
	public static void RemoveTag( GameObject obj, string tag )
	{
		if ( obj.IsValid() )
		{
			obj.Tags.Remove( tag );
		}
	}

	void UpdateGrab( Vector3 eyePos, Rotation eyeRot, Vector3 eyeDir, bool wantsToFreeze )
	{
		var Player = FWPlayerController.Local;

		if ( !Player.IsValid() ) return;

		if ( wantsToFreeze )
		{
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

		if ( body?.GetGameObject()?.Components.TryGet<FortwarsProp>( out var prop ) ?? false )
		{
			prop.IsGrabbed = true;
		}

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
				HeldBody.Velocity = 0;
				HeldBody.AngularVelocity = 0;

				if ( HeldBody.GetGameObject().Components.TryGet<FortwarsProp>( out var prop ) )
				{
					prop.IsGrabbed = false;

					prop.Rigidbody.Enabled = false;
				}
			}
		}

		HeldBody = null;
		Grabbing = false;

		RemoveTag( GrabbedObject, "grabbed" );

		if ( GrabbedObject.IsValid() && GrabbedObject.Components.TryGet<FortwarsProp>( out var fortwarsProp ) && !fortwarsProp.IsHovered )
		{
			var outline = GrabbedObject.Components.Get<HighlightOutline>();

			if ( outline.IsValid() )
				PropHealth.RemoveHighlightOutline( fortwarsProp );
		}

		GrabbedObject = null;
		Scene?.Dispatch( new OnPhysgunGrabChange( false ) );
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
		var player = FWPlayerController.Local;

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

	protected override void OnDisabled()
	{
		if ( IsProxy )
			return;

		GrabEnd();

		MouseInput = false;
		Mouse.Visible = false;
	}

	void UpdateEffects()
	{
		if ( GrabbedObject.IsValid() )
		{
			PhysLine.Enabled = true;
			EndLineObject.WorldPosition = GrabbedObject.GetBounds().Center;

			if ( GrabbedObject.Components.TryGet<FortwarsProp>( out var prop ) && !prop.IsHovered )
				GrabbedObject.Components.GetOrCreate<HighlightOutline>();
			//LineParticles.Enabled = true;
			//LineParticles.WorldPosition = TracerPoint.WorldPosition.MoveTowards( GrabbedPosition, 0.5f );
			////Emitter.Size = Emitter.Size.WithX( GrabbedPosition.Distance( TracerPoint.WorldPosition ) );
			////Emitter.WorldRotation = Rotation.LookAt( TracerPoint.WorldPosition - GrabbedPosition );
		}
		else
		{
			if ( LineParticles.IsValid() )
				LineParticles.Enabled = false;

			if ( PhysLine.IsValid() )
				PhysLine.Enabled = false;
		}
	}
}
