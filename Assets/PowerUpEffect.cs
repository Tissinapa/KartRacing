using UnityEngine;

public abstract class PowerUpEffect : ScriptableObject
{
    public abstract void Apply(GameObject target);
}
