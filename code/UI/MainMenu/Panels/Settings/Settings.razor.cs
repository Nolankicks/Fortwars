using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Sandbox.UI;

public partial class Settings : Panel
{
    public List<SettingAttribute> Attributes { get; set; } = new();

    [Setting]
    public static int SettingTest { get; set; } = 0;

	[Setting]
    public static float SettingTest2 { get; set; } = 5;

	[Setting]
	public static bool SettingTest3 { get; set; } = true;

    void Close()
    {
        var parent = Scene.GetAllComponents<MainMenu>().FirstOrDefault();
        parent.ChangeMenuState(MainMenu.MenuState.None);
    }

    protected override void OnAfterTreeRender(bool firstTime)
    {
        if (firstTime)
        {
            foreach (var type in TypeLibrary.GetTypes<Settings>())
            {
                foreach (var p in type.Properties)
                {
                    var setting = p.GetCustomAttribute<SettingAttribute>();

                    if (setting is null)
                        continue;

                    setting.Type = p.GetValue(this) switch
                    {
                        float _ => SettingType.Float,
                        int _ => SettingType.Int,
                        bool _ => SettingType.Bool,
                        _ => SettingType.Bool,
                    };

                    var value = p.GetValue(this);

					setting.Value = value;

					var conVar = p.GetCustomAttribute<ConVar>();

					if (conVar is not null)
					{
						setting.ConVar = conVar;
					}

                    Attributes.Add(setting);
                }
			}
        }

        base.OnAfterTreeRender(firstTime);
    }

	public void SetInt( string convarName, int value )
	{
		ConsoleSystem.SetValue( convarName, value );

		StateHasChanged();
	}

	public void SetFloat( string convarName, float value )
	{
		ConsoleSystem.SetValue( convarName, value );

		StateHasChanged();
	}

	public void SetBool( string convarName, bool value )
	{
		ConsoleSystem.SetValue( convarName, value );

		StateHasChanged();
	}

	protected override int BuildHash()
	{
		return System.HashCode.Combine( Attributes?.Count() );
	}
}
