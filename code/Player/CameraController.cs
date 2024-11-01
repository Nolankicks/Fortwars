using System;

public sealed class CameraController : Component
{
	public Angles AngleOffset { get; set; }
	public Vector3 PosOffset { get; set; }

	public static CameraController Instance { get; set; }

	public float FOVMult { get; set; } = 1.0f;

	protected override void OnEnabled()
	{
		Instance = this;
	}

	protected override void OnPreRender()
	{
		HandleScreenShake();

		var player = FWPlayerController.Local;

		if ( player.IsRespawning )
		{
			var ragdollPos = player.Ragdoll.IsValid() ? player.Ragdoll.WorldPosition : player.DeathPos;

			Scene.Camera.WorldPosition = ragdollPos + player.EyeAngles.ToRotation().Backward * 250;
			Scene.Camera.WorldRotation = player.EyeAngles;

			if ( player.SetFov )
				Scene.Camera.FieldOfView = (player.OverrideFOV == 0 ? Preferences.FieldOfView : player.OverrideFOV) * FOVMult;

			return;
		}

		WorldPosition = player.Eye.WorldPosition;
		WorldRotation = player.Eye.WorldRotation.Angles() + AngleOffset;

		Scene.Camera.FieldOfView = (player.OverrideFOV == 0 ? Preferences.FieldOfView : player.OverrideFOV) * FOVMult;
	}

	// Credits to SWP for screen shake code
	// https://github.com/timmybo5/simple-weapon-base/blob/master/code/swb_player/PlayerBase.ScreenShake.cs

	ScreenShake lastScreenShake;
	RealTimeSince timeSinceShake;
	float nextShake;

	public void ShakeScreen( ScreenShake screenShake )
	{
		lastScreenShake = screenShake;
		timeSinceShake = 0;
		nextShake = 0;
	}

	public void HandleScreenShake()
	{
		if ( timeSinceShake < lastScreenShake?.Duration && timeSinceShake > nextShake )
		{
			var random = new Random();
			var randomPos = new Vector3( random.Float( 0, lastScreenShake.Size ), random.Float( 0, lastScreenShake.Size ), random.Float( 0, lastScreenShake.Size ) );
			var randomRot = new Angles( random.Float( 0, lastScreenShake.Rotation ), random.Float( 0, lastScreenShake.Rotation ), 0 );

			AngleOffset += randomRot;
			PosOffset += randomPos;
			nextShake = timeSinceShake + lastScreenShake.Delay;
		}
		else
		{
			AngleOffset = Rotation.Identity;
			PosOffset = Vector3.Zero;
		}
	}
}
