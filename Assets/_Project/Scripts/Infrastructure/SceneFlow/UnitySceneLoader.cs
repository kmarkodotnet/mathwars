using UnityEngine.SceneManagement;

namespace MathHorde.Infrastructure.SceneFlow
{
    /// <summary>Production ISceneLoader — the only place in this feature that calls SceneManager directly.</summary>
    public sealed class UnitySceneLoader : ISceneLoader
    {
        public void Load(string sceneName) => SceneManager.LoadScene(sceneName);
    }
}
