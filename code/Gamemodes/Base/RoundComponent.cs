using System;

public sealed class RoundComponent : Component
{
	[Property, ReadOnly] public bool IsRoundActive { get; set; }

	[Header( "Metadata" )]
	[Property] public string Name { get; set; }

	[Property, ToggleGroup( "Condition" )] public bool Condition { get; set; }
	[Property, Group( "Condition" )] public Func<bool> EndCondition { get; set; }
	[Property, Group( "Condition" )] public RoundComponent NextRoundCondition { get; set; }

	[Property, ToggleGroup( "Time" )] public bool Time { get; set; }

	[Property, Group( "Time" )] public float RoundTime { get; set; }

	[Property, Group( "Time" )] public RoundComponent NextRoundTimer { get; set; }


	[Header( "Inventory" )]

	[Property] public bool UseClasses { get; set; }

	[Property, ShowIf( "UseClasses", false )] List<WeaponData> PlayerWeapons { get; set; }


	[Header( "Actions" )]
	[Property, Category( "Actions" )] public Action OnRoundStart { get; set; }
	[Property, Category( "Actions" )] public Action OnRoundEnd { get; set; }

	[InlineEditor, Property] public TimeUntil RoundTimer { get; set; }

	public void ActivateRound()
	{
		OnRoundStart?.Invoke();

		RoundTimer = RoundTime;

		Scene.GetAll<Inventory>()?.ToList()?.ForEach( x => 
		{
			x.ClearAll();

			x.AddItems( PlayerWeapons );
		});

		IsRoundActive = true;

		var instance = GameSystem.Instance;

		if ( instance.IsValid() && instance.CurrentGameModeComponent.IsValid() )
		{
			instance.CurrentGameModeComponent.CurrentRound = this;

			instance.CurrentTime = RoundTime;
		}
	}

	protected override void OnFixedUpdate()
	{
		if ( IsProxy || !IsRoundActive )
			return;

		if ( Time )
		{
			if ( RoundTimer )
			{
				OnRoundEnd?.Invoke();
				NextRoundTimer?.ActivateRound();

				IsRoundActive = false;
			}
		}
	}

}
