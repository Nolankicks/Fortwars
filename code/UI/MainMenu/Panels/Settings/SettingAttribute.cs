using System;

[AttributeUsage(AttributeTargets.Property)]
public sealed class SettingAttribute : Attribute
{
    public SettingType Type { get; set; }
    public object Value { get; set; }
	public string Title { get; set; } = "You forgot to set the title";
	public ConVar ConVar { get; set; }

    public T GetValue<T>()
    {
        return (T)Convert.ChangeType(Value, typeof(T));
    }
}

public enum SettingType
{
	Float,
	Int,
	Bool,
}
