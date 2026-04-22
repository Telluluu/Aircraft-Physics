using UnityEngine;

public class IFF : MonoBehaviour
{
    public enum IFFTeamType
    {
        BlueTeam,
        RedTeam
    }

    public enum IFFObjectType
    {
        Aircraft,
        Missile
    }

    public IFFTeamType affilation;
    public IFFTeamType enemyAffilation;
    public IFFObjectType objectType;
}
