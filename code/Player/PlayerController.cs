using Sandbox.Citizen;
using Sandbox.Events;


public record PlayerDeath( PlayerController Player ) : IGameEvent;

public record PlayerDamage( PlayerController Player, DamageEvent DamageEvent ) : IGameEvent;

public record PlayerReset() : IGameEvent;

public record JumpEvent() : IGameEvent;

public record OnPlayerJoin() : IGameEvent;

public sealed class PlayerController : Component, IGameEventHandler<DamageEvent>, IGameEventHandler<PlayerReset>, IGameEventHandler<DeathEvent>
{
	[Property, Category( "Refrences" )] public ShrimpleCharacterController.ShrimpleCharacterController shrimpleCharacterController { get; set; }
	[Property, Category( "Refrences" ), Sync] public CitizenAnimationHelper AnimHelper { get; set; }
	public Vector3 WishVelocity { get; set; }
	[Sync] public Angles EyeAngles { get; set; }
	[Property, Category( "Refrences" )] public GameObject Eye { get; set; }
	[Property, Sync] public ModelRenderer HoldRenderer { get; set; }
	[Property, Sync] public Inventory Inventory { get; set; }
	[RequireComponent, Sync] public HealthComponent HealthComponent { get; set; }
	[Sync, Change( nameof( OnHoldTypeChanged ) )] public CitizenAnimationHelper.HoldTypes HoldType { get; set; }
	[RequireComponent, Sync] public TeamComponent TeamComponent { get; set; }
	[Property, Sync] public int Kills { get; set; }
	[Property, Sync] public int Deaths { get; set; }
	public bool CanMoveHead = true;

	private static PlayerController _local;
	public static PlayerController Local
	{
		get
		{
			if ( !_local.IsValid() )
				_local = Game.ActiveScene.GetAllComponents<PlayerController>().FirstOrDefault( x => !x.IsProxy );

			return _local;
		}
	}
	TimeSince lastUngrounded;

	[Property, Sync] public Transform RespawnPoint { get; set; }

	public bool IsRespawning { get; set; } = false;

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

		var gs = Scene.GetAll<GameSystem>()?.FirstOrDefault();

		if ( !gs.IsValid() )
			return;

		//MountAllAssets( gs.MountedIndents );
	}


	[Broadcast]
	public static void ClearHoldRenderer( ModelRenderer modelRenderer )
	{
		if ( modelRenderer.IsValid() )
			modelRenderer.Model = Cloud.Model( "vidya.model-none" );
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
			Crouch();
			Move();
		}

		Anims();

		if ( AnimHelper?.Target.IsValid() ?? false )
			AnimHelper.Target.WorldRotation = new Angles( 0, EyeAngles.yaw, 0 ).ToRotation();
	}

	protected override void OnUpdate()
	{
		if ( !IsProxy )
		{
			BuildEyeAngles();
			CameraPos();
			CanMoveHead = true;

			if ( !shrimpleCharacterController.IsValid() )
				return;
		}
	}

	public bool IsCrouching { get; set; } = false;

	bool CanUncrouch()
	{
		if ( !IsCrouching )
			return true;

		var tr = Scene.Trace.Ray( shrimpleCharacterController.WorldPosition, shrimpleCharacterController.WorldPosition + Vector3.Up * 64 )
			.IgnoreGameObject( GameObject )
			.Run();

		return !tr.Hit;
	}

	public float EyeHeight { get; set; } = 64;

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
				shrimpleCharacterController.WorldPosition += Vector3.Up * 32;
				Transform.ClearInterpolation();
				EyeHeight -= 32;
			}

			return;
		}

		if ( !wishCrouch )
		{
			if ( !CanUncrouch() ) return;

			shrimpleCharacterController.TraceHeight = 64;
			IsCrouching = wishCrouch;
			return;
		}
	}

	[Authority]
	public void ResetStats()
	{
		Kills = 0;
		Deaths = 0;
	}

	[Property, Sync] public int WalkSpeed { get; set; } = 300;
	[Property, Sync] public int RunSpeed { get; set; } = 450;
	[Sync, Property] public int StartingWalkSpeed { get; set; } = 300;
	[Sync, Property] public int StartingRunSpeed { get; set; } = 450;

	public float GetMoveSpeed()
	{
		if ( IsCrouching )
			return 100;

		return Input.Down( "run" ) ? RunSpeed : WalkSpeed;
	}

	public void Move()
	{
		if ( !shrimpleCharacterController.IsValid() || IsRespawning )
			return;

		WishVelocity = Input.AnalogMove.Normal * Rotation.FromYaw( EyeAngles.yaw );

		WishVelocity *= GetMoveSpeed();

		if ( !shrimpleCharacterController.IsOnGround )
		{
			lastUngrounded = 0;
		}

		if ( Input.Pressed( "jump" ) && shrimpleCharacterController.IsOnGround )
		{
			shrimpleCharacterController.Punch( Vector3.Up * 350 );
			GameObject.Dispatch( new JumpEvent() );
		}

		shrimpleCharacterController.WishVelocity = WishVelocity;

		shrimpleCharacterController.Move();
	}

	[Broadcast]
	public void BroadcastJump()
	{
		if ( !AnimHelper.IsValid() )
			return;

		AnimHelper.TriggerJump();
	}

	public void Anims()
	{
		if ( !AnimHelper.IsValid() || !shrimpleCharacterController.IsValid() )
			return;

		AnimHelper.WithVelocity( shrimpleCharacterController.Velocity );
		AnimHelper.WithWishVelocity( WishVelocity );
		AnimHelper.IsGrounded = shrimpleCharacterController.IsOnGround;
		AnimHelper.WithLook( EyeAngles.Forward * 100, 1, 0.5f, 0.25f );

		AnimHelper.DuckLevel = IsCrouching ? 1 : 0;
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

	public void CameraPos()
	{
		if ( !Scene?.Camera.IsValid() ?? false || !Eye.IsValid() )
			return;

		if ( IsRespawning )
		{
			var lookRot = EyeAngles.ToRotation();

			Scene.Camera.WorldPosition = DeathPos + Vector3.Up * 64 + lookRot.Backward * 200;
			Scene.Camera.WorldRotation = lookRot;

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
		camera.WorldPosition = targetCameraPos;
		camera.WorldRotation = EyeAngles;
		camera.FieldOfView = Preferences.FieldOfView;
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

		foreach ( var model in Components.GetAll<ModelRenderer>().Where( x => x.Tags.Has( "clothing" ) ) )
		{
			model.RenderType = renderType;
		}

		if ( HoldRenderer.IsValid() )
			HoldRenderer.RenderType = renderType;
	}

	void IGameEventHandler<DamageEvent>.OnGameEvent( DamageEvent eventArgs )
	{
		var pc = eventArgs.Attacker?.Root?.Components?.Get<PlayerController>();

		if ( !pc.IsValid() )
			return;

		Scene.Dispatch( new PlayerDamage( this, eventArgs ) );
	}

	[Authority]
	public void AddKills( int amount )
	{
		Kills += amount;
	}

	[Authority]
	public void AddDeaths( int amount )
	{
		Deaths += amount;
	}

	[Broadcast]
	public void BroadcastDeathMessage()
	{
		Scene.Dispatch( new PlayerDeath( this ) );
	}

	void IGameEventHandler<PlayerReset>.OnGameEvent( PlayerReset eventArgs )
	{
		if ( IsProxy )
			return;

		if ( HealthComponent.IsValid() )
			HealthComponent.ResetHealth();

		Log.Info( "Player Reset" );
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
	public void SetWorld( Transform transform )
	{
		EyeAngles = WorldRotation.Angles();

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
	public void TeleportToTeamSpawnPoint()
	{
		var spawns = Scene.GetAll<TeamSpawnPoint>()?.Where( x => x.Team == TeamComponent.Team ).ToList();

		if ( spawns is null || spawns.Count == 0 )
		{
			SetWorld( RespawnPoint );
			return;
		}

		var spawn = Game.Random.FromList( spawns );

		SetWorld( spawn.Transform.World );
	}

	public Vector3 DeathPos { get; set; }

	void IGameEventHandler<DeathEvent>.OnGameEvent( DeathEvent eventArgs )
	{
		var pc = eventArgs.Attacker?.Root?.Components?.Get<PlayerController>();

		if ( IsProxy )
			return;

		if ( Inventory.IsValid() )
			Inventory.ResetAmmo();

		AddDeaths( 1 );

		BroadcastDeathMessage();

		Log.Info( "Player Death" );

		if ( pc.IsValid() )
			pc.AddKills( 1 );

		var gs = GameSystem.Instance;

		if ( gs.IsValid() )
			gs.AddKill();

		if ( AnimHelper?.Target.IsValid() ?? false )
		{
			if ( !Inventory.IsValid() )
				return;

			Inventory.DisableAll();
			Inventory.CanSwitch = false;

			var target = AnimHelper.Target;

			var go = new GameObject( true, $"{Network.Owner?.DisplayName}'s ragdoll" );
			go.Tags.Add( "ragdoll" );
			go.WorldTransform = target.WorldTransform;
			go.WorldRotation = target.WorldRotation;

			var ragdollBody = go.Components.Create<SkinnedModelRenderer>();
			ragdollBody.CopyFrom( target );
			ragdollBody.UseAnimGraph = false;

			foreach ( var clothing in target.GameObject.Children.SelectMany( x => x.Components.GetAll<SkinnedModelRenderer>() ) )
			{
				var newClothing = new GameObject( true, clothing.GameObject.Name );
				newClothing.Parent = go;

				var clothingRenderer = newClothing.Components.Create<SkinnedModelRenderer>();
				clothingRenderer.CopyFrom( clothing );
				clothingRenderer.BoneMergeTarget = ragdollBody;
			}

			var modelPhys = go.Components.Create<ModelPhysics>();
			modelPhys.Model = ragdollBody.Model;
			modelPhys.Renderer = ragdollBody;
			modelPhys.CopyBonesFrom( target, true );

			go.Components.Create<RagdollComponent>();

			go.Network.SetOwnerTransfer( OwnerTransfer.Takeover );

			go.NetworkSpawn();

			BroadcastEnable( target.GameObject, false );

			IsRespawning = true;

			DeathPos = target.WorldPosition;

			TeleportToTeamSpawnPoint();

			if ( Components.TryGet<NameTag>( out var tag, FindMode.EnabledInSelfAndChildren ) )
			{
				BroadcastEnable( tag.GameObject, false );
			}

			Invoke( 2, () =>
			{
				Inventory.CanSwitch = true;
				Inventory.ChangeItem( Inventory.Index, Inventory?.Items );

				BroadcastEnable( target.GameObject, true );

				if ( tag.IsValid() )
					BroadcastEnable( tag.GameObject, true );

				IsRespawning = false;

				GameObject.Dispatch( new PlayerReset() );
			} );
		}
	}

	[Broadcast]
	public static void BroadcastEnable( GameObject go, bool enable )
	{
		go.Enabled = enable;
	}

    /*[Broadcast]
	public async void MountAllAssets( List<string> indents )
	{
		foreach ( var asset in indents )
		{
			var package = await Package.Fetch( asset, false );

			if ( package is null )
				return;

			await package.MountAsync();
		}

		Log.Info( "Mounted all assets" );
	}/*/

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

		if ( gs.State == GameSystem.GameState.OvertimeFight || gs.State == GameSystem.GameState.FightMode )
			TeleportToTeamSpawnPoint();
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
}
