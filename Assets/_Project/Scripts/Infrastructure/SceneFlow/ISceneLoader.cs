namespace MathHorde.Infrastructure.SceneFlow
{
    /// <summary>
    /// Thin seam over UnityEngine.SceneManagement.SceneManager so
    /// SceneFlowManager's routing decision (which AppState loads which
    /// scene) is testable without a real scene in Build Settings.
    /// </summary>
    public interface ISceneLoader
    {
        void Load(string sceneName);
    }
}
