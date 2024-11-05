using Sandbox.UI;

public partial class Settings : Panel
{
	void Close()
	{
		var parent = Scene.GetAllComponents<MainMenu>().FirstOrDefault();
		parent.ChangeMenuState( MainMenu.MenuState.None );
	}
}
