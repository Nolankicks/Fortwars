public static class DevCheck
{
	public static bool IsDev( ulong steamid )
	{
		List<ulong> DevList = new() { 76561198965897085, 76561199001645276 };
		return DevList.Contains( steamid );
	}
}
