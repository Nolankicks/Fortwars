using Sandbox.Citizen;
using Sandbox.Events;
using Sandbox.Services;

[Title( "FW - Player Controller" )]
public sealed partial class FWPlayerController : Component, IGameEventHandler<DamageEvent>, IGameEventHandler<PlayerReset>,
IGameEventHandler<DeathEvent>, IGameEventHandler<OnPhysgunGrabChange>
{
	//Refrences
	[Property, Category( "References" ), Sync] public CitizenAnimationHelper AnimHelper { get; set; }
	[Property, Category( "References" )] public GameObject Eye { get; set; }
	[Property, Category( "References" )] public CapsuleCollider Hitbox { get; set; }
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
	public bool IsRespawning { get; set; } = false;
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

	protected override void OnStart()
	{
		if ( !AnimHelper.IsValid() )
			return;

		AnimHelper.HoldType = HoldType;

		if ( IsProxy )
			return;

		StartingWalkSpeed = WalkSpeed;
		StartingRunSpeed = RunSpeed;

		RespawnPoint = Transform.World;
	}

	[Broadcast]
	public static void ClearHoldRenderer( ModelRenderer modelRenderer )
	{
		Log.Info( "Clearing hold renderer" );

		if ( modelRenderer.IsValid() )
			modelRenderer.GameObject.Enabled = false;
	}

	private void OnHoldTypeChanged( CitizenAnimationHelper.HoldTypes oldValue, CitizenAnimationHelper.HoldTypes newValue )
	{
		if ( !AnimHelper.IsValid() )
			return;

		AnimHelper.HoldType = newValue;
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
		if ( AnimHelper?.Target.IsValid() ?? false )
			AnimHelper.Target.WorldRotation = new Angles( 0, EyeAngles.yaw, 0 ).ToRotation();
	}

	[Authority]
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

	[Broadcast]
	public void BroadcastJump()
	{
		if ( !AnimHelper.IsValid() )
			return;

		AnimHelper.TriggerJump();
	}

	public void UpdateAnimation()
	{
		if ( !AnimHelper.IsValid() )
			return;
		AnimHelper.WithLook( EyeAngles.Forward * 100, 1, 0.5f, 0.25f );
	}

	public void BuildEyeAngles()
	{
		var ee = EyeAngles;
		if ( CanMoveHead )
		{
			ee += Input.AnalogLook;
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

	[Broadcast]
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

	[Authority]
	public void AddKills( int amount )
	{
		if ( IsProxy )
			return;

		Kills += amount;

		//Increment kills stat
		Stats.Increment( "kills_new", 1 );
	}

	[Authority]
	public void AddDeaths( int amount )
	{
		if ( IsProxy )
			return;

		Deaths += amount;

		//Increment deaths stat
		Stats.Increment( "deaths_new", 1 );
	}

	[Broadcast]
	public void BroadcastDeathMessage( GameObject attacker )
	{
		Scene.Dispatch( new PlayerDeath( this, attacker ) );
	}

	[Authority]
	public void OnRestart()
	{
		if ( HealthComponent.IsValid() )
			HealthComponent.ResetHealth();

		Kills = 0;

		Deaths = 0;
	}

	[Authority]
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
			Local.HealthComponent.TakeDamage( Local.GameObject, 100 );
	}

	[Authority]
	public void TeleportToTeamSpawnPoint( bool changeEyeAngles = true )
	{
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
	}

	[Authority]
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

	[Broadcast]
	public static void BroadcastEnable( GameObject go, bool enable )
	{
		go.Enabled = enable;
	}

	[Authority]
	public void SetSpeed( int walkSpeed, int runSpeed )
	{
		WalkSpeed = walkSpeed;
		RunSpeed = runSpeed;
	}

	[Authority]
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

	[Authority]
	public void ResetMaxPlayerHealth()
	{
		if ( !HealthComponent.IsValid() )
			return;

		HealthComponent.Health = 100;
		HealthComponent.MaxHealth = 100;
	}

	[Authority]
	public void ResetSpeed()
	{
		Log.Info( "Resetting speed" );

		WalkSpeed = StartingWalkSpeed;
		RunSpeed = StartingRunSpeed;
	}

	[Authority]
	public void SetVotedMap( MapInfo map )
	{
		VotedForMap = map;
	}
}
