using Sandbox.UI.Construct;
using System;
using Sandbox.UI;
using Sandbox.Citizen;

public class ModelPanel : Panel
{
	readonly ScenePanel scenePanel;

	Angles CamAngles = new( 14f, -21f, 0.0f );
	float CamDistance = 94;
	Vector3 CamPos => CamAngles.Forward * -CamDistance;

	public SceneModel Citizen { get; set; }

	public SceneWorld World => scenePanel.World;

	public PlayerClass PlayerClass { get; set; }

	public SceneModel Gun { get; set; }

	public List<SceneModel> ClothingModels { get; set; } = new();

	public ModelPanel()
	{
		Style.FlexWrap = Wrap.Wrap;
		Style.JustifyContent = Justify.Center;
		Style.AlignItems = Align.Center;
		Style.AlignContent = Align.Center;
		Style.Padding = 0;

		var world = new SceneWorld();
		scenePanel = new ScenePanel();
		scenePanel.World = world;
		scenePanel.Camera.Position = CamPos;
		scenePanel.Camera.Rotation = Rotation.From( CamAngles );
		scenePanel.Camera.FieldOfView = 70;

		scenePanel.Style.Width = Length.Percent( 100 );
		scenePanel.Style.Height = Length.Percent( 100 );

		AddChild( scenePanel );

		Citizen = new SceneModel( world, "models/citizen/citizen.vmdl", new Transform( Vector3.Up * -32.0f, Rotation.From( 0, 180, 0 ) ) );

		var clothes = ClothingContainer.CreateFromLocalUser();
		
		var SkinMaterial = clothes.Clothing.Select( x => x.Clothing.SkinMaterial ).Where( x => !string.IsNullOrWhiteSpace( x ) ).Select( x => Material.Load( x ) ).FirstOrDefault();
		var EyesMaterial = clothes.Clothing.Select( x => x.Clothing.EyesMaterial ).Where( x => !string.IsNullOrWhiteSpace( x ) ).Select( x => Material.Load( x ) ).FirstOrDefault();

		Citizen.SetMaterialOverride( SkinMaterial, "skin" );
		Citizen.SetMaterialOverride( EyesMaterial, "eyes" );

		foreach ( var entry in clothes.Clothing )
		{
			var c = entry.Clothing;

			var modelPath = c.GetModel( clothes.Clothing.Select( x => x.Clothing ) );

			if ( string.IsNullOrEmpty( modelPath ) || !string.IsNullOrEmpty( c.SkinMaterial ) )
				continue;

			var model = Model.Load( modelPath );
			if ( !model.IsValid() || model.IsError )
				continue;

			var r = new SceneModel( world, model, Transform.Zero );

			if ( SkinMaterial is not null ) r.SetMaterialOverride( SkinMaterial, "skin" );
			if ( EyesMaterial is not null ) r.SetMaterialOverride( EyesMaterial, "eyes" );

			if ( !string.IsNullOrEmpty( c.MaterialGroup ) )
				r.SetMaterialGroup( c.MaterialGroup );

			if ( c.AllowTintSelect )
			{
				var tintValue = entry.Tint?.Clamp( 0, 1 ) ?? c.TintDefault;
				var tintColor = c.TintSelection.Evaluate( tintValue );
				r.ColorTint = tintColor;
			}

			ClothingModels.Add( r );
		}

		foreach ( var (name, value) in clothes.GetBodyGroups( clothes.Clothing.Select( x => x.Clothing ) ) )
		{
			Citizen.SetBodyGroup( name, value );
		}


		new SceneLight( world, Vector3.Up * 150.0f, 200.0f, Color.White * 2.0f );
		new SceneLight( world, Vector3.Up * 150.0f + Vector3.Backward * 100.0f, 200, Color.White * 5.0f );
		new SceneLight( world, Vector3.Up * 10.0f + Vector3.Right * 100.0f, 200, Color.White * 5.0f );
		new SceneLight( world, Vector3.Up * 10.0f + Vector3.Left * 100.0f, 200, Color.White * 5.0f );

		Gun = new SceneModel( world, Cloud.Model( "facepunch.w_mp5" ), new Transform( Vector3.Up * -50.0f, Rotation.From( 0, 180, 0 ) ) );
	}

	public override void Tick()
	{
		base.Tick();

		scenePanel.Camera.Position = CamPos;
		scenePanel.Camera.Rotation = Rotation.From( CamAngles );

		foreach ( var renderer in scenePanel.World.SceneObjects.OfType<SceneModel>() )
		{
			renderer.Update( RealTime.Delta );
		}

		if ( !Citizen.IsValid() )
			return;

		foreach ( var renderer in ClothingModels )
		{
			renderer.MergeBones( Citizen );
		}

		var mousePos = MousePosition;
		var headPos = scenePanel.Camera.ToScreen( (Citizen.GetAttachment( "eyes (on bone head)" ) ?? Transform.Zero).Position );
		var localPos = mousePos - headPos;

		//Citizen.SetAnimParameter( "aim_eyes", SetLookDirection( new Vector3( CamDistance, localPos.x, -localPos.y ), 1 ) );
		//Citizen.SetAnimParameter( "aim_body", SetLookDirection( new Vector3( CamDistance, localPos.x, -localPos.y ), 1f ) );
		//Citizen.SetAnimParameter( "aim_head", SetLookDirection( new Vector3( CamDistance, localPos.x, -localPos.y ), 1f ) );

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
