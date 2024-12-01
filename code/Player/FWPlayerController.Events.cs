using Sandbox.Events;

public record PlayerDeath( FWPlayerController Player, GameObject Attacker ) : IGameEvent;
public record PlayerDamage( FWPlayerController Player, DamageEvent DamageEvent ) : IGameEvent;
public record PlayerReset() : IGameEvent;
public record JumpEvent() : IGameEvent;
public record OnPlayerJoin() : IGameEvent;
public record OnPhysgunGrabChange( bool isCurrentlyGrabbing ) : IGameEvent;

partial class FWPlayerController
{
	void IGameEventHandler<PlayerReset>.OnGameEvent( PlayerReset eventArgs )
	{
		if ( IsProxy )
			return;

		if ( HealthComponent.IsValid() )
			HealthComponent.ResetHealth();

		if ( Scene.GetAllComponents<GameMode>().FirstOrDefault().TeamsEnabled )
			TeleportToTeamSpawnPoint();
		else
			TeleportToAnySpawnPoint();
	}

	void IGameEventHandler<DamageEvent>.OnGameEvent( DamageEvent eventArgs )
	{
		if ( IsProxy )
			return;

		Sound.Play( "flesh.hit" );

		Scene.Dispatch( new PlayerDamage( this, eventArgs ) );
	}

	void IGameEventHandler<DeathEvent>.OnGameEvent( DeathEvent eventArgs )
	{
		if ( IsProxy )
			return;

		var pc = eventArgs.Attacker?.Root?.Components?.Get<FWPlayerController>();

		//Make sure we are only calling this
		if ( IsProxy )
			return;

		if ( Inventory.IsValid() )
			Inventory.ResetAmmo();

		AddDeaths( 1 );

		//Broadcast death message
		BroadcastDeathMessage( eventArgs.Attacker );

		//Add kills to attacker
		if ( pc.IsValid() )
			pc.AddKills( 1 );

		var gs = GameSystem.Instance;

		if ( gs.IsValid() )
			gs.AddKill();


		if ( AnimHelper?.Target.IsValid() ?? false )
		{
			Inventory.DisableAll();
			Inventory.CanScrollSwitch = false;
			Inventory.CanPickUp = false;

			var deathPos = AnimHelper.Target.WorldTransform;

			DeathPos = deathPos.Position;

			TeleportToTeamSpawnPoint( false );

			if ( !Inventory.IsValid() )
				return;

			if ( Components.TryGet<HighlightOutline>( out var highlightOutline ) )
			{
				PropHealth.RemoveHighlightOutline( highlightOutline );
				GameObject.Network.Refresh();
			}

			var mode = Scene.GetAll<GameMode>()?.FirstOrDefault();

			var target = AnimHelper.Target;

			var go = new GameObject( true, $"{Network.Owner?.DisplayName}'s ragdoll" );
			go.Tags.Add( FW.Tags.Ragdoll );
			go.WorldTransform = deathPos;

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

			Ragdoll = go;

			go.NetworkSpawn( null );

			BroadcastEnable( target.GameObject, false );

			if ( mode.IsValid() && !mode.RespawnPlayers )
			{
				IsSpectating = true;

				if ( shrimpleCharacterController.IsValid() )
					shrimpleCharacterController.Enabled = false;

				return;
			}

			IsRespawning = true;

			if ( Components.TryGet<NameTag>( out var tag, FindMode.EnabledInSelfAndChildren ) )
			{
				BroadcastEnable( tag.GameObject, false );
			}

			Invoke( 2, () =>
			{
				BroadcastEnable( target.GameObject, true );

				if ( tag.IsValid() )
					BroadcastEnable( tag.GameObject, true );

				RespawnPlayer();
			} );
		}
	}

	[Rpc.Owner]
	public void RespawnPlayer()
	{
		Inventory.CanScrollSwitch = true;
		Inventory.ChangeItem( Inventory.Index, Inventory?.Items );
		Inventory.CanPickUp = true;

		IsRespawning = false;
		IsSpectating = false;
		HasFlag = false;

		EyeAngles = WorldRotation.Angles();

		GameObject.Dispatch( new PlayerReset() );

		if ( shrimpleCharacterController.IsValid() )
			shrimpleCharacterController.Enabled = true;

		if ( AnimHelper.Target.IsValid() )
			BroadcastEnable( AnimHelper.Target.GameObject, true );

		Ragdoll = null;
	}

	void IGameEventHandler<OnPhysgunGrabChange>.OnGameEvent( OnPhysgunGrabChange eventArgs )
	{
		if ( IsProxy )
			return;

		Inventory.CanScrollSwitch = !eventArgs.isCurrentlyGrabbing;
	}
}
