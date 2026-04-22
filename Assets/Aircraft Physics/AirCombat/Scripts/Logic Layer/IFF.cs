using UnityEngine;

public class IFF : MonoBehaviour
{
    public enum IFFType
    {
        BlueTeam,
        RedTeam
    }

    public IFFType affilation;
    public IFFType enemyAffilation;
}
