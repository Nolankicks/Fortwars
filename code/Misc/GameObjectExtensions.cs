public static class GameObjectExtensions
{
	public static PlayerController GetLocalPlayer( this GameObject go )
	{
		var player = Game.ActiveScene.GetAllComponents<PlayerController>().FirstOrDefault( p => !p.IsProxy );
		return player;
	}

	public static PlayerController GetLocalPlayer( this Component comp )
	{
		var player = Game.ActiveScene.GetAllComponents<PlayerController>().FirstOrDefault( p => !p.IsProxy );
		return player;
	}
}
