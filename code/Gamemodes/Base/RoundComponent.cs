using System;

public sealed class RoundComponent : Component
{
	[Property] public string Name { get; set; }

	enum EndConditions
	{
		[Title( "Timer" ), Icon( "Timer" ),
			Description( "End the round when the timer runs out" )]
		E_TIMER,
		[Title( "Condition" ), Icon( "Receipt" ),
		Description( "End the round when a team reaches a certain score (DONE VIA ACTION)" )]
		E_CONDITION,
	}
	[Property] EndConditions EndCondition { get; set; }

	[Property] public bool UseClasses { get; set; }

	[Property, ShowIf( "UseClasses", false )] List<WeaponData> PlayerWeapons { get; set; }

	[Property, Category( "Actions" )] public Action OnRoundStart { get; set; }
	[Property, Category( "Actions" )] public Action OnRoundEnd { get; set; }

	protected override void OnUpdate()
	{

	}
}
