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
	[Property, Category( "Actions" )] public Action RoundUpdate { get; set; }

	[InlineEditor, Property] public TimeUntil RoundTimer { get; set; }

	[Header( "We can add a new type each time we want a new round")]
	[Property] public GameSystem.GameState State { get; set; }

	public void ActivateRound()
	{
		Log.Info( "Activating round: " + Name );

		OnRoundStart?.Invoke();

		RoundTimer = RoundTime;

		Scene.GetAll<Inventory>()?.ToList()?.ForEach( x =>
		{
			x.ClearAll();

			x.AddItems( PlayerWeapons );
		} );

		IsRoundActive = true;

		var instance = GameSystem.Instance;

		if ( instance.IsValid() && instance.CurrentGameModeComponent.IsValid() )
		{
			instance.CurrentGameModeComponent.CurrentRound = this;

			instance.CurrentTime = RoundTime;

			instance.CurrentGameModeComponent.DispatchEvent( State );
		}
	}

	protected override void OnFixedUpdate()
	{
		if ( IsProxy || !IsRoundActive )
			return;

		RoundUpdate?.Invoke();

		if ( Time )
		{
			if ( RoundTimer )
			{
				EndRound();
			}
		}

		if ( Condition )
		{
			if ( EndCondition?.Invoke() ?? false )
			{
				EndRound();
			}
		}
	}

	public void EndRound()
	{
		OnRoundEnd?.Invoke();

		if ( Condition )
			NextRoundCondition?.ActivateRound();
		else
			NextRoundTimer?.ActivateRound();

		IsRoundActive = false;
	}

	[ActionGraphNode( "50 / 50" ), Pure]
	public static bool FiftyFifty()
	{
		return Game.Random.Float( 0, 1 ) > 0.5f;
	}

}
