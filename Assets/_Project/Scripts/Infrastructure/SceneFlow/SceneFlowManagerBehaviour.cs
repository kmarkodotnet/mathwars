using System;
using System.Collections.Generic;
using MathHorde.GameLogic.AppFlow;
using UnityEngine;

namespace MathHorde.Infrastructure.SceneFlow
{
    /// <summary>
    /// Unity adapter for SceneFlowManager. Everything it does is lifecycle
    /// translation, not a decision: build the AppState→scene-name lookup
    /// from Inspector data and hand Unity's OnApplicationPause callback to
    /// the plain-C# SceneFlowManager, which is the only place that decides
    /// what happens (unity-csharp skill). No business logic lives here.
    ///
    /// Call Initialize once from the scene's composition root with the
    /// session's single IAppStateMachine instance (same pattern as
    /// GameplayHUD.Initialize / EnemyPresenter.Initialize) — not from
    /// Awake/Start, since the state machine instance is owned elsewhere.
    /// </summary>
    public sealed class SceneFlowManagerBehaviour : MonoBehaviour
    {
        [SerializeField] private SceneBinding[] _sceneBindings = Array.Empty<SceneBinding>();

        private SceneFlowManager _sceneFlowManager;

        public void Initialize(IAppStateMachine stateMachine)
        {
            var sceneByState = new Dictionary<AppState, string>();
            foreach (SceneBinding binding in _sceneBindings)
            {
                if (binding != null)
                {
                    sceneByState[binding.State] = binding.SceneName;
                }
            }

            _sceneFlowManager?.Dispose();
            _sceneFlowManager = new SceneFlowManager(stateMachine, new UnitySceneLoader(), sceneByState);
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            _sceneFlowManager?.HandleApplicationPause(pauseStatus);
        }

        private void OnDestroy()
        {
            _sceneFlowManager?.Dispose();
        }
    }
}
