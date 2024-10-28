using Sandbox.Events;

public record PlayerDeath( PlayerController Player, GameObject Attacker ) : IGameEvent;
public record PlayerDamage( PlayerController Player, DamageEvent DamageEvent ) : IGameEvent;
public record PlayerReset() : IGameEvent;
public record JumpEvent() : IGameEvent;
public record OnPlayerJoin() : IGameEvent;
public record OnPhysgunGrabChange( bool isCurrentlyGrabbing ) : IGameEvent;

partial class PlayerController
{
	void IGameEventHandler<PlayerReset>.OnGameEvent( PlayerReset eventArgs )
	{
		if ( IsProxy )
			return;

		if ( HealthComponent.IsValid() )
			HealthComponent.ResetHealth();

		Log.Info( "Player Reset" );
	}

	void IGameEventHandler<DamageEvent>.OnGameEvent( DamageEvent eventArgs )
	{
		if ( IsProxy )
			return;

		Scene.Dispatch( new PlayerDamage( this, eventArgs ) );
	}

	void IGameEventHandler<DeathEvent>.OnGameEvent( DeathEvent eventArgs )
	{
		var pc = eventArgs.Attacker?.Root?.Components?.Get<PlayerController>();

		//Make sure we are only calling this
		if ( IsProxy )
			return;

		if ( Inventory.IsValid() )
			Inventory.ResetAmmo();

		AddDeaths( 1 );

		//Broadcast death message
		BroadcastDeathMessage( eventArgs.Attacker );

		Log.Info( "Player Death" );

		//Add kills to attacker
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
			Inventory.CanScrollSwitch = false;

			var target = AnimHelper.Target;

			var go = new GameObject( true, $"{Network.Owner?.DisplayName}'s ragdoll" );
			go.Tags.Add( FW.Tags.Ragdoll );
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

			if ( shrimpleCharacterController.IsValid() )
				modelPhys.PhysicsGroup.AddVelocity( shrimpleCharacterController.Velocity );

			go.Components.Create<RagdollComponent>();

			go.Network.SetOwnerTransfer( OwnerTransfer.Takeover );

			go.NetworkSpawn();

			BroadcastEnable( target.GameObject, false );

			IsRespawning = true;

			DeathPos = target.WorldPosition;

			TeleportToTeamSpawnPoint( false );

			if ( Components.TryGet<NameTag>( out var tag, FindMode.EnabledInSelfAndChildren ) )
			{
				BroadcastEnable( tag.GameObject, false );
			}

			Ragdoll = go;

			Invoke( 2, () =>
			{
				Inventory.CanScrollSwitch = true;
				Inventory.ChangeItem( Inventory.Index, Inventory?.Items );

				BroadcastEnable( target.GameObject, true );

				if ( tag.IsValid() )
					BroadcastEnable( tag.GameObject, true );

				IsRespawning = false;

				EyeAngles = WorldRotation.Angles();

				GameObject.Dispatch( new PlayerReset() );

				Ragdoll = null;
			} );
		}
	}

	void IGameEventHandler<OnPhysgunGrabChange>.OnGameEvent( OnPhysgunGrabChange eventArgs )
	{
		if ( IsProxy )
			return;

		Inventory.CanScrollSwitch = !eventArgs.isCurrentlyGrabbing;
	}
}
