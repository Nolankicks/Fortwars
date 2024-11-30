using Sandbox.UI.Construct;
using System;
using Sandbox.UI;
using Sandbox.Citizen;

public class ModelPanel : Panel
{
	readonly ScenePanel scenePanel;

	Angles CamAngles = new( 0.0f, 0.0f, 0.0f );
	float CamDistance = 200;
	Vector3 CamPos => CamAngles.Forward * -CamDistance;

	public SceneModel Citizen { get; set; }

	public SceneWorld World => scenePanel.World;

	public PlayerClass PlayerClass => ResourceLibrary.Get<PlayerClass>( "classes/jugg.class" );

	public SceneModel Gun { get; set; }

	public ModelPanel()
	{
		Style.FlexWrap = Wrap.Wrap;
		Style.JustifyContent = Justify.Center;
		Style.AlignItems = Align.Center;
		Style.AlignContent = Align.Center;
		Style.Padding = 0;
		Style.Width = Length.Percent( 100 );
		Style.Height = Length.Percent( 100 );
		Style.PointerEvents = PointerEvents.All;

		var world = new SceneWorld();
		scenePanel = new ScenePanel();
		scenePanel.World = world;
		scenePanel.Camera.Position = CamPos;
		scenePanel.Camera.Rotation = Rotation.From( CamAngles );
		scenePanel.Camera.FieldOfView = 70;

		scenePanel.Style.Width = Length.Percent( 100 );
		scenePanel.Style.Height = Length.Percent( 100 );

		AddChild( scenePanel );

		Citizen = new SceneModel( world, "models/citizen/citizen.vmdl", new Transform( Vector3.Up * -50.0f, Rotation.From( 0, 180, 0 ) ) );

		new SceneDirectionalLight( world, Rotation.From( 45, 0, 0 ), Color.White );

		Gun = new SceneModel( world, Cloud.Model( "facepunch.w_mp5" ), new Transform( Vector3.Up * -50.0f, Rotation.From( 0, 180, 0 ) ) );
	}

	public override void Tick()
	{
		base.Tick();

		scenePanel.Camera.Position = CamPos;
		scenePanel.Camera.Rotation = Rotation.From( CamAngles );

		if ( !Citizen.IsValid() )
			return;

		Citizen.Update( RealTime.Delta );

		var mousePos = MousePosition;
		var headPos = scenePanel.Camera.ToScreen( (Citizen.GetAttachment( "eyes (on bone head)" ) ?? Transform.Zero).Position );
		var localPos = mousePos - headPos;

		Citizen.SetAnimParameter( "aim_eyes", SetLookDirection( new Vector3( 200, localPos.x, -localPos.y ), 1 ) );
		Citizen.SetAnimParameter( "aim_body", SetLookDirection( new Vector3( 200, localPos.x, -localPos.y ), 0.3f ) );
		Citizen.SetAnimParameter( "aim_head", SetLookDirection( new Vector3( 200, localPos.x, -localPos.y ), 0.6f ) );

		var light = World.SceneObjects.OfType<SceneDirectionalLight>().FirstOrDefault();

		if ( light is not null )
		{
			light.Rotation = Rotation.From( 0, 0, 0 );
		}

		if ( PlayerClass is null || (!PlayerClass?.ClassModelEnabled ?? true) )
		{
			Citizen.SetAnimParameter( "holdtype", 0 );

			Gun?.Delete();

			return;
		}

		Citizen.SetAnimParameter( "holdtype", (int)PlayerClass.HoldType );

		if ( Gun is null )
		{
			Gun = new SceneModel( World, PlayerClass.ClassModel, Transform.Zero );

			return;
		}

		Gun.Model = PlayerClass.ClassModel;

		var hold = Citizen.GetAttachment( "hold_r" ).GetValueOrDefault();

		Gun.Transform = new Transform( hold.Position + PlayerClass.Offset, hold.Rotation );
	}

	public Vector3 SetLookDirection( Vector3 eyeDirectionWorld, float weight )
	{
		return eyeDirectionWorld * weight;
	}
}
