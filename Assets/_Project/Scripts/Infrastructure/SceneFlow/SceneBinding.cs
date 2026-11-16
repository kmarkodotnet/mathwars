using System;
using MathHorde.GameLogic.AppFlow;
using UnityEngine;

namespace MathHorde.Infrastructure.SceneFlow
{
    /// <summary>
    /// Inspector-configurable AppState → scene-name pair (spec 16.: no
    /// scene name baked into code). SceneFlowManagerBehaviour builds the
    /// lookup SceneFlowManager uses from an array of these.
    /// </summary>
    [Serializable]
    public sealed class SceneBinding
    {
        [SerializeField] private AppState _state;
        [SerializeField] private string _sceneName;

        public AppState State => _state;
        public string SceneName => _sceneName;
    }
}
