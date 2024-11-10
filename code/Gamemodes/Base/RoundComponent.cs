using System;

public sealed class RoundComponent : Component
{
	[Header( "Metadata" )]
	[Property] public string Name { get; set; }



	enum EndTypes
	{
		[Title( "Timer" ), Icon( "Timer" ),
			Description( "End the round when the timer runs out" )]
		E_TIMER,
		[Title( "Condition" ), Icon( "Receipt" ),
		Description( "End the round when a team reaches a certain score (DONE VIA ACTION)" )]
		E_CONDITION,
	}
	[Header( "End Conditions" )]
	[Property] EndTypes EndType { get; set; }

	[Property, ShowIf( "EndType", EndTypes.E_CONDITION )] public Func<bool> EndCondition { get; set; }

	[Header( "Inventory" )]

	[Property] public bool UseClasses { get; set; }

	[Property, ShowIf( "UseClasses", false )] List<WeaponData> PlayerWeapons { get; set; }


	[Header( "Actions" )]
	[Property, Category( "Actions" )] public Action OnRoundStart { get; set; }
	[Property, Category( "Actions" )] public Action OnRoundEnd { get; set; }

}
