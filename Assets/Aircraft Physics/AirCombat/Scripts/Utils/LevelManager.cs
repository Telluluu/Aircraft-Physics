using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gamelogic
{
    public class LevelManager : Singleton<LevelManager>
    {
        private void Start()
        {
            DontDestroyOnLoad(gameObject);
        }

        public void NextLevel()
        {
            var idx = SceneManager.GetActiveScene().buildIndex;
            int nextIdx = idx + 1;
            SceneManager.LoadSceneAsync(nextIdx);
        }

        public void ReturnToTitle()
        {
            Debug.Log("ReturnToTitle");
            string currentSceneName = SceneManager.GetActiveScene().name;
            if (currentSceneName == "Scenes/Title_Tilemap")
                Restart();
            else
                SceneManager.LoadSceneAsync("Scenes/Title_Tilemap");
        }

        public void Restart()
        {
            Debug.Log("Restart");
            string currentSceneName = SceneManager.GetActiveScene().name;
            SceneManager.LoadSceneAsync(currentSceneName);
        }
    }
}
