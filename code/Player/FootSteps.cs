
using Sandbox.Audio;
using ShrimpleCharacterController;

public sealed class Footsteps : Component
{
	[Property] SkinnedModelRenderer Source { get; set; }
	[Property] ShrimpleCharacterController.ShrimpleCharacterController CharacterController { get; set; }
	[Property] MixerHandle FootstepMixer { get; set; }

	protected override void OnEnabled()
	{
		if ( Source is null )
			return;

		Source.OnFootstepEvent += OnFootstepEvent;
	}

	protected override void OnDisabled()
	{
		if ( Source is null )
			return;

		Source.OnFootstepEvent -= OnFootstepEvent;
	}

	TimeSince timeSinceStep;

	private void OnFootstepEvent( SceneModel.FootstepEvent e )
	{
		if ( !CharacterController.IsOnGround ) return;
		if ( timeSinceStep < 0.2f ) return;

		timeSinceStep = 0;

		PlayFootstepSound( e.Transform.Position, e.Volume, e.FootId );
	}

	private void PlayFootstepSound( Vector3 worldPosition, float volume, int foot )
	{
		var tr = Scene.Trace
			.Ray( worldPosition + Vector3.Up * 10, worldPosition + Vector3.Down * 20 )
			.IgnoreGameObjectHierarchy( GameObject )
			.Run();

		if ( !tr.Hit || tr.Surface is null )
			return;

		var sound = foot == 0 ? tr.Surface.Sounds.FootLeft : tr.Surface.Sounds.FootRight;
		var soundEvent = ResourceLibrary.Get<SoundEvent>( sound );

		if ( soundEvent is null )
			return;

		var handle = GameObject.PlaySound( soundEvent, 0 );
		handle.TargetMixer = FootstepMixer.GetOrDefault();
		handle.Volume *= volume * 2;
	}
}
