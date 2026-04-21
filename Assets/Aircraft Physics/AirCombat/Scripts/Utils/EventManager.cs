using UnityEngine;
using UnityEngine.Events;

namespace Gamelogic
{
    public class EventManager : Singleton<EventManager>
    {
        private UnityEvent onPlayerLaunchMissile = new UnityEvent();

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }
    }
}
