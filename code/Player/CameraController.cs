public sealed class CameraController : Component
{
	public Angles AngleOffset { get; set; }
	public Vector3 PosOffset { get; set; }

	public static CameraController Instance { get; set; }

	public float FOVMult { get; set; } = 1.0f;

	public float FOVMultTarget { get; set; } = 1.0f;

	private Rotation CurrentRotation { get; set; }
	private Rotation TargetRotation { get; set; }

	[Property] private Vector3 Recoil { get; set; }
	[Property] private float Snappiness { get; set; }
	[Property] private float ReturnSpeed { get; set; }

	protected override void OnEnabled()
	{
		Instance = this;
	}

	protected override void OnPreRender()
	{
		var player = FWPlayerController.Local;

		if ( IsProxy )
			return;

		if ( !player.IsValid() || !Scene.Camera.IsValid() )
			return;

		FOVMult = FOVMult.LerpTo( FOVMultTarget, 10.0f * Time.Delta );

		if ( player.IsRespawning )
		{
			var ragdollPos = player.Ragdoll.IsValid() ? player.Ragdoll.WorldPosition : player.DeathPos;

			Scene.Camera.WorldPosition = ragdollPos + player.EyeAngles.ToRotation().Backward * 250;
			Scene.Camera.WorldRotation = player.EyeAngles;

			if ( player.SetFov )
				Scene.Camera.FieldOfView = (player.OverrideFOV == 0 ? Preferences.FieldOfView : player.OverrideFOV) * FOVMult;

			return;
		}

		UpdateRecoil();

		WorldPosition = player.Eye.WorldPosition;
		WorldRotation = player.Eye.WorldRotation.Angles() + AngleOffset;

		Scene.Camera.FieldOfView = (player.OverrideFOV == 0 ? Preferences.FieldOfView : player.OverrideFOV) * FOVMult;
	}

	void UpdateRecoil()
	{
		TargetRotation = Rotation.Lerp( TargetRotation, Rotation.Identity, ReturnSpeed * Time.Delta );
		CurrentRotation = Rotation.Slerp( CurrentRotation, TargetRotation, Snappiness * Time.Delta );
		AngleOffset = CurrentRotation;
	}

	public void RecoilFire( Vector3 recoil )
	{
		TargetRotation += new Angles( recoil.x, Game.Random.Float( -recoil.y, recoil.y ), Game.Random.Float( -recoil.z, recoil.z ) ).ToRotation() * 0.5f;
	}
}
