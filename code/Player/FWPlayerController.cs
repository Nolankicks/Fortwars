using Sandbox.Citizen;
using Sandbox.Events;
using Sandbox.Services;
using System;

[Title( "FW - Player Controller" )]
public sealed partial class FWPlayerController : Component, IGameEventHandler<DamageEvent>, IGameEventHandler<PlayerReset>,
IGameEventHandler<DeathEvent>, IGameEventHandler<OnPhysgunGrabChange>
{
	//Refrences
	[Property, Category( "References" )] public ShrimpleCharacterController.ShrimpleCharacterController shrimpleCharacterController { get; set; }
	[Property, Category( "References" ), Sync] public CitizenAnimationHelper AnimHelper { get; set; }
	[Property, Category( "References" )] public GameObject Eye { get; set; }
	[Property, Sync, Category( "References" )] public ModelRenderer HoldRenderer { get; set; }
	[Property, Sync, Category( "References" )] public Inventory Inventory { get; set; }

	//Stats
	[Property, Sync, Category( "Stats" )] public int WalkSpeed { get; set; } = 300;
	[Property, Sync, Category( "Stats" )] public int RunSpeed { get; set; } = 450;
	[Property, Sync, Category( "Stats" ), ReadOnly] public int Kills { get; set; }
	[Property, Sync, Category( "Stats" ), ReadOnly] public int Deaths { get; set; }

	//Saved properties
	[Sync] public int StartingWalkSpeed { get; set; } = 300;
	[Sync] public int StartingRunSpeed { get; set; } = 450;

	[Sync] public Angles EyeAngles { get; set; }

	//Required components
	[RequireComponent, Sync] public HealthComponent HealthComponent { get; set; }
	[RequireComponent, Sync] public TeamComponent TeamComponent { get; set; }

	[Sync, Change( nameof( OnHoldTypeChanged ) )] public CitizenAnimationHelper.HoldTypes HoldType { get; set; }
	[Sync] public Transform RespawnPoint { get; set; }
	[Sync] public bool IsCrouching { get; set; } = false;
	public Vector3 DeathPos { get; set; }
	[Sync] public bool IsRespawning { get; set; } = false;
	public Vector3 WishVelocity { get; set; }
	public bool CanMoveHead = true;
	public float EyeHeight { get; set; } = 64;
	public bool SetFov { get; set; } = true;
	private static FWPlayerController _local;
	public static FWPlayerController Local
	{
		get
		{
			if ( !_local.IsValid() )
				_local = Game.ActiveScene.GetAllComponents<FWPlayerController>().FirstOrDefault( x => !x.IsProxy );

			return _local;
		}
	}

	private TimeSince lastUngrounded;
	private bool LastOnGround = true;

	/// <summary>
	/// How many times we have crouched mid-air. Used to prevent spamming when we adjust the Z pos of the player.
	/// </summary>
	private int airCrouchCount;

	public float OverrideFOV = 0;

	public float SpeedMult { get; set; } = 1.0f;

	public SceneTraceResult ViewTrace => Scene.Trace.Ray( Eye.WorldPosition, Eye.WorldPosition + EyeAngles.Forward * 1000 ).WithoutTags( FW.Tags.NoBuild, FW.Tags.Player ).Run();

	//TODO RESET THIS SHIT!!!!
	[Sync] public MapInfo VotedForMap { get; set; }

	[Sync] public bool IsSpectating { get; set; } = false;

	public static bool ShownAboutPanel { get; set; } = false;

	[Property] public int WoodPropsLeft { get; set; } = 60;
	[Property] public int MetalPropsLeft { get; set; } = 30;
	[Property] public int SteelPropsLeft { get; set; } = 15;

	public bool IsADS { get; set; } = false;

	protected override void OnStart()
	{
		if ( !AnimHelper.IsValid() )
			return;

		AnimHelper.HoldType = HoldType;

		if ( IsProxy )
			return;

		var gs = GameSystem.Instance;

		if ( gs.IsValid() )
		{
			WoodPropsLeft = gs.WoodProps;
			MetalPropsLeft = gs.MetalProps;
			SteelPropsLeft = gs.SteelProps;
		}

		StartingWalkSpeed = WalkSpeed;
		StartingRunSpeed = RunSpeed;

		RespawnPoint = Transform.World;

		/*var hud = HUD.Instance;

		if ( hud.IsValid() && !ShownAboutPanel )
		{
			hud?.Panel.AddChild( new AboutPanel() );

			ShownAboutPanel = true;
		}*/
	}

	[Rpc.Broadcast]
	public static void ClearHoldRenderer( ModelRenderer modelRenderer )
	{
		if ( modelRenderer.IsValid() )
			modelRenderer.GameObject.Enabled = false;
	}

	private void OnHoldTypeChanged( CitizenAnimationHelper.HoldTypes oldValue, CitizenAnimationHelper.HoldTypes newValue )
	{
		if ( !AnimHelper.IsValid() )
			return;

		AnimHelper.HoldType = newValue;
	}

	protected override void OnFixedUpdate()
	{
		if ( !IsProxy )
		{
			if ( !IsSpectating )
			{
				Crouch();
				Move();
			}
			else
			{
				Spectate();
			}

			if ( shrimpleCharacterController.IsOnGround && !LastOnGround && !IsSpectating )
				CameraController.Instance.RecoilFire( new Vector3( 5, 0, 0 ) );

			LastOnGround = shrimpleCharacterController.IsOnGround;
		}
	}

	protected override void OnUpdate()
	{
		if ( !IsProxy )
		{
			BuildEyeAngles();
			PositionCamera();
			CanMoveHead = true;
		}

		UpdateAnimation();

		if ( (AnimHelper?.Target.IsValid() ?? false) && !IsSpectating )
			AnimHelper.Target.WorldRotation = new Angles( 0, EyeAngles.yaw, 0 ).ToRotation();
	}

	public void Crouch()
	{
		var wishCrouch = Input.Down( "duck" );

		if ( wishCrouch == IsCrouching )
			return;

		// crouch
		if ( wishCrouch )
		{
			shrimpleCharacterController.TraceHeight = 36;
			IsCrouching = wishCrouch;

			if ( !shrimpleCharacterController.IsOnGround )
			{
				if ( airCrouchCount < 1 )
				{
					shrimpleCharacterController.WorldPosition += Vector3.Up * 32;
					airCrouchCount += 1;
				}

				Transform.ClearInterpolation();
				EyeHeight -= 32;
			}

			return;
		}

		bool canUnCrouch()
		{
			if ( !IsCrouching )
				return true;

			var tr = Scene.Trace.Ray( shrimpleCharacterController.WorldPosition, shrimpleCharacterController.WorldPosition + Vector3.Up * 64 )
				.IgnoreGameObject( GameObject )
				.Run();

			return !tr.Hit;
		}

		if ( !wishCrouch )
		{
			if ( !canUnCrouch() ) return;

			shrimpleCharacterController.TraceHeight = 64;
			IsCrouching = wishCrouch;
			return;
		}
	}

	[Rpc.Owner]
	public void ResetStats()
	{
		Kills = 0;
		Deaths = 0;
	}

	public float GetMoveSpeed()
	{
		if ( IsCrouching )
			return 100;

		return (Input.Down( "run" ) ? RunSpeed : WalkSpeed) * SpeedMult;
	}

	public void Move()
	{
		if ( !shrimpleCharacterController.IsValid() || IsRespawning )
			return;

		var forwardDirection = EyeAngles.WithPitch( 0f ).Forward;
		var velocityDirection = shrimpleCharacterController.Velocity.WithZ( 0f ).Normal;
		var dotProduct = Math.Max( Vector3.Dot( forwardDirection, velocityDirection ), 0.5f );
		var sprintingCoefficient = MathX.Remap( dotProduct, 0.5f, 1f, 0f, 1f );

		var sprintSpeed = MathX.Lerp( WalkSpeed, GetMoveSpeed(), sprintingCoefficient );

		var isCrouching = Input.Down( "duck" );
		var wishSpeed = isCrouching ? 175 : sprintSpeed;

		wishSpeed = IsADS ? 160 : wishSpeed;

		var wishDirection = Input.AnalogMove.Normal * Rotation.FromYaw( EyeAngles.yaw );
		WishVelocity = wishDirection * wishSpeed;

		if ( !shrimpleCharacterController.IsOnGround )
		{
			lastUngrounded = 0;
		}
		else
		{
			airCrouchCount = 0;
		}

		if ( Input.Pressed( "jump" ) && shrimpleCharacterController.IsOnGround )
		{
			shrimpleCharacterController.Punch( Vector3.Up * 350 );
			GameObject.Dispatch( new JumpEvent() );
			CameraController.Instance.RecoilFire( new Vector3( 10, 0, 0 ) );
		}

		shrimpleCharacterController.WishVelocity = WishVelocity;

		shrimpleCharacterController.Move();
	}

	public void Spectate()
	{
		WishVelocity = new Angles( EyeAngles.pitch, EyeAngles.yaw, 0 ).ToRotation() * Input.AnalogMove.Normal;

		WishVelocity *= GetMoveSpeed() * 2;

		if ( !WishVelocity.IsNearlyZero() )
		{
			WorldPosition += WishVelocity * Time.Delta;
		}
	}

	[Rpc.Broadcast]
	public void BroadcastJump()
	{
		if ( !AnimHelper.IsValid() )
			return;

		AnimHelper.TriggerJump();
	}

	public void UpdateAnimation()
	{
		if ( !AnimHelper.IsValid() || !shrimpleCharacterController.IsValid() )
			return;

		AnimHelper.WithVelocity( shrimpleCharacterController.Velocity );
		AnimHelper.WithWishVelocity( WishVelocity );
		AnimHelper.IsGrounded = shrimpleCharacterController.IsOnGround;
		AnimHelper.WithLook( EyeAngles.Forward * 100, 1, 0.5f, 0.25f );

		AnimHelper.DuckLevel = IsCrouching ? 1 : 0;

		// Match the hitbox size to our animation.
		// DuckLevel doesn't seem to work during jumping so let's just not bother if we aren't grounded.
		/*if ( IsCrouching && shrimpleCharacterController.IsOnGround )
			Hitbox.End = Vector3.Up * 32.0f;
		else
			Hitbox.End = Vector3.Up * 56.0f;*/
	}

	public void BuildEyeAngles()
	{
		var ee = EyeAngles;
		if ( CanMoveHead )
		{
			ee += Input.AnalogLook * CameraController.Instance.FOVMult;
		}

		ee.pitch = ee.pitch.Clamp( -89, 89 );

		ee.roll = 0;

		EyeAngles = ee;

		Eye.WorldRotation = ee.ToRotation();
	}

	public GameObject Ragdoll { get; set; }

	public void PositionCamera()
	{
		if ( !Scene?.Camera.IsValid() ?? false || !Eye.IsValid() )
			return;

		if ( IsRespawning )
		{
			var ragdollPos = Ragdoll.IsValid() ? Ragdoll.WorldPosition : DeathPos;

			Scene.Camera.WorldPosition = ragdollPos + EyeAngles.ToRotation().Backward * 250;
			Scene.Camera.WorldRotation = EyeAngles;

			if ( SetFov )
				Scene.Camera.FieldOfView = OverrideFOV == 0 ? Preferences.FieldOfView : OverrideFOV;

			return;
		}

		var camera = Scene.GetAllComponents<CameraComponent>().Where( x => x.IsMainCamera ).FirstOrDefault();

		if ( !camera.IsValid() )
			return;

		var targetEyeHeight = IsCrouching ? 28 : 64;
		EyeHeight = EyeHeight.LerpTo( targetEyeHeight, RealTime.Delta * 10.0f );

		var targetCameraPos = WorldPosition + new Vector3( 0, 0, EyeHeight );

		if ( lastUngrounded > 0.1f )
		{
			targetCameraPos.z = camera.WorldPosition.z.LerpTo( targetCameraPos.z, RealTime.Delta * 25.0f );
		}

		Eye.WorldPosition = targetCameraPos;
		//camera.WorldPosition = targetCameraPos;
		//camera.WorldRotation = EyeAngles;
		//camera.FieldOfView = OverrideFOV == 0 ? Preferences.FieldOfView : OverrideFOV;
	}

	[Rpc.Broadcast]
	public void BroadcastAttack()
	{
		if ( !AnimHelper.IsValid() || !AnimHelper.Target.IsValid() )
			return;

		AnimHelper.Target.Set( "b_attack", true );
	}

	protected override void OnPreRender()
	{
		var renderType = IsProxy ? ModelRenderer.ShadowRenderType.On : ModelRenderer.ShadowRenderType.ShadowsOnly;

		var target = AnimHelper.Target;

		if ( target.IsValid() )
			target.RenderType = renderType;

		foreach ( var model in Components.GetAll<ModelRenderer>().Where( x => x.Tags.Has( FW.Tags.Clothing ) ) )
		{
			model.RenderType = renderType;
		}

		if ( HoldRenderer.IsValid() )
			HoldRenderer.RenderType = renderType;
	}

	[Rpc.Owner]
	public void AddKills( int amount )
	{
		if ( IsProxy )
			return;

		Kills += amount;

		//Increment kills stat
		Stats.Increment( "kills_new", 1 );
	}

	[Rpc.Owner]
	public void AddDeaths( int amount )
	{
		if ( IsProxy )
			return;

		Deaths += amount;

		//Increment deaths stat
		Stats.Increment( "deaths_new", 1 );
	}

	[Rpc.Broadcast]
	public void BroadcastDeathMessage( GameObject attacker )
	{
		Scene.Dispatch( new PlayerDeath( this, attacker ) );
	}

	[Rpc.Owner]
	public void OnRestart()
	{
		if ( HealthComponent.IsValid() )
			HealthComponent.ResetHealth();

		Kills = 0;

		Deaths = 0;
	}

	[Rpc.Owner]
	public void SetWorld( Transform transform, bool ChangeAngles = true )
	{
		Transform.ClearInterpolation();

		if ( ChangeAngles )
			EyeAngles = transform.Rotation.Angles();

		Transform.World = transform;
	}

	[ConCmd( "kill" )]
	public static void KillPlayer()
	{
		if ( !Local.IsValid() )
			return;

		if ( Local.IsProxy )
			return;

		if ( Local.IsValid() )
			Local.HealthComponent.TakeDamage( Local.GameObject, Local.HealthComponent.Health );
	}

	[Rpc.Owner]
	public void TeleportToTeamSpawnPoint( bool changeEyeAngles = true )
	{
		if ( HealthComponent.IsValid() )
			HealthComponent.AbleToTakeDamage = false;

		List<TeamSpawnPoint> Spawns;

		Spawns = Scene.GetAll<TeamSpawnPoint>()?.Where( x => x.Team == TeamComponent.Team )?.ToList();

		if ( Spawns is null || Spawns?.Count == 0 )
		{
			SetWorld( RespawnPoint, changeEyeAngles );
			return;
		}

		if ( GameSystem.Instance.IsValid() )
		{
			var spawn = Game.Random.FromList( Spawns );

			SetWorld( spawn.Transform.World, changeEyeAngles );
		}

		if ( HealthComponent.IsValid() )
			HealthComponent.AbleToTakeDamage = true;
	}

	[Rpc.Owner]
	public void TeleportToAnySpawnPoint()
	{
		var teamSpawns = Scene.GetAll<TeamSpawnPoint>()
			.Where( x => x.Team == Team.None )
			.Select( x => x.GameObject )
			.ToList();

		var spawns = Scene.GetAll<SpawnPoint>()
			.Select( x => x.GameObject )
			.ToList();

		var allSpawns = new List<GameObject>();
		allSpawns.AddRange( teamSpawns );
		allSpawns.AddRange( spawns );

		Transform SpawnTransform = allSpawns.Count > 0 ? Game.Random.FromList( allSpawns ).Transform.World : Transform.World;

		SetWorld( SpawnTransform );
	}

	[Sync] public bool HasFlag { get; set; } = true;

	protected override void OnDisabled()
	{
		if ( HasFlag )
			RoundComponent.SpawnNewFlag( TeamComponent.Team == Team.Red ? Team.Blue : Team.Red );
	}

	[Rpc.Broadcast]
	public static void BroadcastEnable( GameObject go, bool enable )
	{
		if ( go.IsValid() )
			go.Enabled = enable;
	}

	[Rpc.Owner]
	public void SetSpeed( int walkSpeed, int runSpeed )
	{
		WalkSpeed = walkSpeed;
		RunSpeed = runSpeed;
	}

	[Rpc.Owner]
	public void SetPlayerMaxHealth( int health )
	{
		if ( !HealthComponent.IsValid() )
			return;

		HealthComponent.MaxHealth = health;
		HealthComponent.Health = health;

		var gs = GameSystem.Instance;

		if ( !gs.IsValid() )
			return;
	}

	[Rpc.Owner]
	public void ResetMaxPlayerHealth()
	{
		if ( !HealthComponent.IsValid() )
			return;

		HealthComponent.Health = 100;
		HealthComponent.MaxHealth = 100;
	}

	[Rpc.Owner]
	public void ResetSpeed()
	{
		Log.Info( "Resetting speed" );

		WalkSpeed = StartingWalkSpeed;
		RunSpeed = StartingRunSpeed;
	}

	[Rpc.Owner]
	public void SetVotedMap( MapInfo map )
	{
		VotedForMap = map;
	}

	[Rpc.Owner]
	public void ResetResouces()
	{
		var gs = GameSystem.Instance;

		if ( !gs.IsValid() )
			return;

		MetalPropsLeft = gs.MetalProps;
		WoodPropsLeft = gs.WoodProps;
		SteelPropsLeft = gs.SteelProps;
	}

	[Rpc.Owner]
	public void SetHasFlag( bool value )
	{
		HasFlag = value;
	}
}
