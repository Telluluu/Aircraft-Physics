using UnityEngine;
using UnityEngine.Events;

namespace Gamelogic
{
    public class EventManager : Singleton<EventManager>
    {
        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }
    }
}
