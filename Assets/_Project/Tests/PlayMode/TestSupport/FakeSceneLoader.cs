using System.Collections.Generic;
using MathHorde.Infrastructure.SceneFlow;

namespace MathHorde.Tests.PlayMode.TestSupport
{
    /// <summary>Records every Load("sceneName") call instead of touching SceneManager — Build Settings has no scenes yet (F1-11), and the routing decision under test does not need a real one.</summary>
    public sealed class FakeSceneLoader : ISceneLoader
    {
        public List<string> LoadedScenes { get; } = new List<string>();

        public void Load(string sceneName)
        {
            LoadedScenes.Add(sceneName);
        }
    }
}
