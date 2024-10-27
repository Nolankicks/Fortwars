
public partial class EmoteUI : PanelComponent
{
	[Property, TextArea] public string MyStringValue { get; set; } = "Hello World!";

	[Property] private List<EmoteDefinition> ChatEmotes { get; set; } = new List<EmoteDefinition>();

	/// <summary>
	/// the hash determines if the system should be rebuilt. If it changes, it will be rebuilt
	/// </summary>
	protected override int BuildHash() => System.HashCode.Combine( MyStringValue );

	protected override void OnUpdate()
	{
		base.OnUpdate();
		for ( int i = 0; i < ChatEmotes.Count; i++ )
		{
			var emote = ChatEmotes[i];
			if ( emote.Icon == null )
				continue;

			if ( Input.Pressed( "Slot" + (i + 1).ToString() ) )
			{
				Log.Info( "test" );
			}
		}
	}

	void DoEmote( EmoteDefinition emote )
	{
		Log.Info( $"Emote: {emote.Name}" );
		var localPlayer = PlayerController.Local;

		if ( localPlayer.IsValid() )
		{
			var go = GameObject.Clone( "prefabs/WorldEmote.prefab" );
			go.WorldPosition = localPlayer.WorldPosition + Vector3.Up * 64.0f;

			go.NetworkSpawn();
		}
	}
}

public struct EmoteDefinition
{
	[Property] public string Name { get; set; }

	[Property] public Texture Icon { get; set; }
}

