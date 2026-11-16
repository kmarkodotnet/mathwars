// EnemyPresenter.cs
// F1-14 — subscribes to the GameLogic enemy events (EnemySpawned/
// EnemyDamaged/EnemyKilled, contract game-logic.md 1.) and maps them to
// pooled EnemyView instances. Purely visual: it never decides whether an
// enemy is alive, how much health it has, or what it is worth — it only
// reads the InstanceId/EnemyId/Position/RewardValue fields the event already
// carries and reflects them on screen (unity-ui skill: "a Presenter csak
// vizuális állapotot állít be").
//
// Pooling (math-horde.md 18.): every visual key's ObjectPool<EnemyView> is
// prewarmed once in Initialize(), BEFORE any combat event can arrive, so the
// steady-state EnemySpawned/EnemyKilled handlers never call Instantiate/
// Destroy — only Get()/Release() on an already-warm pool. An unrecognised
// EnemyId still resolves to the catalog's fallback pool (EnemyVisualKeyMap),
// so a later-added enemy type never renders as "nothing".
using System.Collections.Generic;
using UnityEngine;
using MathHorde.Core.Common;
using MathHorde.GameLogic.Events;
using MathHorde.Presentation.Events;
using MathHorde.Presentation.Pooling;

namespace MathHorde.Presentation.Enemies
{
    public sealed class EnemyPresenter : MonoBehaviour
    {
        [SerializeField] private EnemyVisualCatalogSO _catalog;
        [SerializeField] private Transform _poolRoot;

        private IGameEventSource _eventSource;
        private EnemyVisualKeyMap _visualKeyMap;
        private readonly Dictionary<string, ObjectPool<EnemyView>> _poolsByVisualKey =
            new Dictionary<string, ObjectPool<EnemyView>>();
        private readonly Dictionary<int, ActiveEnemy> _activeByInstanceId = new Dictionary<int, ActiveEnemy>();

        private readonly struct ActiveEnemy
        {
            public readonly string VisualKey;
            public readonly EnemyView View;

            public ActiveEnemy(string visualKey, EnemyView view)
            {
                VisualKey = visualKey;
                View = view;
            }
        }

        /// <summary>
        /// Wires the presenter to the event source and prewarms every known
        /// visual-key pool up front. Call this once, before the session's
        /// combat loop starts (e.g. from the scene's composition root) — not
        /// from Update().
        /// </summary>
        public void Initialize(IGameEventSource eventSource)
        {
            _eventSource = eventSource;
            _visualKeyMap = _catalog.BuildKeyMap();

            foreach (string visualKey in _visualKeyMap.AllVisualKeys)
            {
                GetOrCreatePool(visualKey);
            }

            _eventSource.Subscribe<EnemySpawned>(HandleEnemySpawned);
            _eventSource.Subscribe<EnemyDamaged>(HandleEnemyDamaged);
            _eventSource.Subscribe<EnemyKilled>(HandleEnemyKilled);
        }

        private void OnDestroy()
        {
            if (_eventSource == null)
            {
                return;
            }

            _eventSource.Unsubscribe<EnemySpawned>(HandleEnemySpawned);
            _eventSource.Unsubscribe<EnemyDamaged>(HandleEnemyDamaged);
            _eventSource.Unsubscribe<EnemyKilled>(HandleEnemyKilled);
        }

        private void HandleEnemySpawned(EnemySpawned e)
        {
            string visualKey = _visualKeyMap.Resolve(e.EnemyId);
            ObjectPool<EnemyView> pool = GetOrCreatePool(visualKey);
            EnemyView view = pool.Get();
            view.Bind(e.EnemyInstanceId, ToWorldPosition(e.Position));
            _activeByInstanceId[e.EnemyInstanceId] = new ActiveEnemy(visualKey, view);
        }

        private void HandleEnemyDamaged(EnemyDamaged e)
        {
            if (_activeByInstanceId.TryGetValue(e.EnemyInstanceId, out ActiveEnemy active))
            {
                active.View.PlayHitFlash();
            }
        }

        private void HandleEnemyKilled(EnemyKilled e)
        {
            if (!_activeByInstanceId.TryGetValue(e.EnemyInstanceId, out ActiveEnemy active))
            {
                return;
            }

            _activeByInstanceId.Remove(e.EnemyInstanceId);
            active.View.PlayDeathPop(() => ReleaseToPool(active.VisualKey, active.View));
        }

        private void ReleaseToPool(string visualKey, EnemyView view)
        {
            view.Unbind();
            if (_poolsByVisualKey.TryGetValue(visualKey, out ObjectPool<EnemyView> pool))
            {
                pool.Release(view);
            }
        }

        private ObjectPool<EnemyView> GetOrCreatePool(string visualKey)
        {
            if (_poolsByVisualKey.TryGetValue(visualKey, out ObjectPool<EnemyView> existing))
            {
                return existing;
            }

            EnemyVisualCatalogSO.Entry entry = _catalog.FindByVisualKey(visualKey);
            EnemyView prefab = entry != null ? entry.Prefab : null;
            int prewarmCount = entry != null ? entry.PrewarmCount : 0;

            if (prefab == null)
            {
                Debug.LogWarning($"[EnemyPresenter] No prefab configured for visual key \"{visualKey}\" — enemies of this type will not render.");
            }

            var pool = new ObjectPool<EnemyView>(
                factory: () => InstantiateView(prefab),
                onGet: view => { },
                onRelease: view => { },
                prewarmCount: prefab != null ? prewarmCount : 0);

            _poolsByVisualKey[visualKey] = pool;
            return pool;
        }

        private EnemyView InstantiateView(EnemyView prefab)
        {
            EnemyView instance = Instantiate(prefab, _poolRoot != null ? _poolRoot : transform);
            instance.Unbind();
            return instance;
        }

        private static Vector3 ToWorldPosition(Vec2 position)
        {
            // Vec2: X = lane axis, Y = depth/forward (core-domain.md 1.) — maps
            // directly onto the arena's XZ ground plane the ArenaBuilder laid out.
            return new Vector3(position.X, 0f, position.Y);
        }
    }
}
