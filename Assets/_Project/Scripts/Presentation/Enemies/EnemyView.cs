// EnemyView.cs
// F1-14 — purely visual, pooled representation of one live enemy instance.
// Owns no combat/health state: CurrentHealth/MaxHealth/IsAlive live in
// GameLogic's EnemySnapshot (core-domain.md / game-logic.md ADR-009) and
// never get duplicated here. This component only knows how to show/hide
// itself, follow a world position, and play two purely cosmetic reactions
// (hit flash, death pop) that EnemyPresenter triggers from GameLogic events —
// it never decides whether a hit happened or how much damage it was.
//
// Animation runs via two small per-frame timers in Update(), not a
// coroutine/allocation per hit — only arithmetic on existing fields, no
// `new`, no LINQ, no string work, honouring math-horde.md 18. (nincs
// allokáció Update()-ben). A flash/pop is only ever playing for a handful of
// pooled, currently-active instances, never one per frame across the whole
// pool.
using UnityEngine;

namespace MathHorde.Presentation.Enemies
{
    [DisallowMultipleComponent]
    public sealed class EnemyView : MonoBehaviour
    {
        [Tooltip("A testet alkotó rendererek — ezekre fut a becsapódás-felvillanás.")]
        [SerializeField] private Renderer[] _flashRenderers = System.Array.Empty<Renderer>();

        [SerializeField] private float _flashDuration = 0.12f;
        [SerializeField] private float _deathPopDuration = 0.18f;

        private Color[] _baseColors;
        private Material[] _instancedMaterials;

        private bool _isFlashing;
        private float _flashElapsed;

        private bool _isPopping;
        private float _popElapsed;
        private System.Action _popCompletedCallback;

        /// <summary>The GameLogic EnemyInstanceId currently bound to this view, or -1 when idle in the pool.</summary>
        public int InstanceId { get; private set; } = -1;

        private void Awake()
        {
            CacheInstancedMaterials();
        }

        /// <summary>Activates the view for a freshly spawned enemy instance. Called by EnemyPresenter, never by combat/business code.</summary>
        public void Bind(int instanceId, Vector3 worldPosition)
        {
            InstanceId = instanceId;
            transform.position = worldPosition;
            transform.localScale = Vector3.one;
            _isFlashing = false;
            _isPopping = false;
            _popCompletedCallback = null;
            ResetFlashColors();
            gameObject.SetActive(true);
        }

        /// <summary>Deactivates and clears binding before the instance returns to the ObjectPool free list.</summary>
        public void Unbind()
        {
            InstanceId = -1;
            _isFlashing = false;
            _isPopping = false;
            _popCompletedCallback = null;
            ResetFlashColors();
            gameObject.SetActive(false);
        }

        public void SetPosition(Vector3 worldPosition)
        {
            transform.position = worldPosition;
        }

        /// <summary>Brief cosmetic flash in reaction to an EnemyDamaged event — no health/business meaning.</summary>
        public void PlayHitFlash()
        {
            _isFlashing = true;
            _flashElapsed = 0f;
        }

        /// <summary>Brief cosmetic shrink-and-vanish in reaction to an EnemyKilled event, then hands control back via onComplete (EnemyPresenter releases the view to its pool there).</summary>
        public void PlayDeathPop(System.Action onComplete)
        {
            _isPopping = true;
            _popElapsed = 0f;
            _popCompletedCallback = onComplete;
        }

        private void Update()
        {
            if (_isFlashing)
            {
                _flashElapsed += Time.deltaTime;
                float t = Mathf.Clamp01(_flashElapsed / Mathf.Max(0.0001f, _flashDuration));
                ApplyFlash(t);
                if (t >= 1f)
                {
                    _isFlashing = false;
                }
            }

            if (_isPopping)
            {
                _popElapsed += Time.deltaTime;
                float t = Mathf.Clamp01(_popElapsed / Mathf.Max(0.0001f, _deathPopDuration));
                float scale = 1f - t;
                transform.localScale = new Vector3(scale, scale, scale);
                if (t >= 1f)
                {
                    _isPopping = false;
                    System.Action callback = _popCompletedCallback;
                    _popCompletedCallback = null;
                    callback?.Invoke();
                }
            }
        }

        private void CacheInstancedMaterials()
        {
            _baseColors = new Color[_flashRenderers.Length];
            _instancedMaterials = new Material[_flashRenderers.Length];
            for (int i = 0; i < _flashRenderers.Length; i++)
            {
                Renderer renderer = _flashRenderers[i];
                if (renderer == null)
                {
                    continue;
                }

                // .material (not .sharedMaterial) instances a per-renderer copy so the
                // flash never mutates the shared asset other pooled siblings use.
                Material instanced = renderer.material;
                _instancedMaterials[i] = instanced;
                _baseColors[i] = ReadColor(instanced);
            }
        }

        private void ResetFlashColors()
        {
            for (int i = 0; i < _instancedMaterials.Length; i++)
            {
                if (_instancedMaterials[i] != null)
                {
                    WriteColor(_instancedMaterials[i], _baseColors[i]);
                }
            }
        }

        private void ApplyFlash(float t)
        {
            // Bright white at impact, easing back to the base color.
            for (int i = 0; i < _instancedMaterials.Length; i++)
            {
                Material mat = _instancedMaterials[i];
                if (mat == null)
                {
                    continue;
                }

                Color flashed = Color.Lerp(Color.white, _baseColors[i], t);
                WriteColor(mat, flashed);
            }
        }

        private static Color ReadColor(Material mat)
        {
            if (mat.HasProperty("_BaseColor"))
            {
                return mat.GetColor("_BaseColor");
            }

            if (mat.HasProperty("_Color"))
            {
                return mat.GetColor("_Color");
            }

            return Color.white;
        }

        private static void WriteColor(Material mat, Color color)
        {
            if (mat.HasProperty("_BaseColor"))
            {
                mat.SetColor("_BaseColor", color);
            }
            else if (mat.HasProperty("_Color"))
            {
                mat.SetColor("_Color", color);
            }
        }
    }
}
