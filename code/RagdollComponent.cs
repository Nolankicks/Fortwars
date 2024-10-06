using Sandbox;

public sealed class RagdollComponent : Component
{
    protected override void OnStart()
    {
        if ( IsProxy )
            return;

        Invoke( 15, GameObject.Destroy );
    }
}
