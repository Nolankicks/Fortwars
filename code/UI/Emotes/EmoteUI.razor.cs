
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
				DoEmote( PlayerController.Local, i );
			}
		}
	}

	[Broadcast]
	void DoEmote( PlayerController player, int index )
	{
		var emote = ChatEmotes[index];
		Log.Info( $"Emote: {emote.Name}" );

		if ( player.IsValid() )
		{
			var go = GameObject.Clone( "prefabs/WorldEmote.prefab" );
			go.WorldPosition = player.WorldPosition + Vector3.Up * 96.0f;

			go.Components.Get<SpriteRenderer>( FindMode.InDescendants ).Texture = emote.Icon;
		}
	}
}

public struct EmoteDefinition
{
	[Property] public string Name { get; set; }

	[Property] public Texture Icon { get; set; }
}

