// ArenaConfigSO.cs
// F1-12 — a 3D pálya-geometria konfigurálható paraméterei.
//
// Kizárólag megjelenítési/elrendezési adatot tartalmaz, üzleti/harci logikát NEM
// (unity-csharp skill: "Konfigurálható játékelem = ScriptableObject", "nincs beégetett
// szám a kódban"). A sávszám a GAME_DESIGN.md szerint a jövőben 2-4 között változhat
// ("A sávszám nem mehet 4 fölé"), ezért ez nincs a generáló eszközbe (ArenaBuilder)
// beégetve — ebből az assetből olvassa ki.
using UnityEngine;

namespace MathHorde.Presentation.Arena
{
    [CreateAssetMenu(fileName = "ArenaConfig", menuName = "Math Horde/Arena/Arena Config")]
    public sealed class ArenaConfigSO : ScriptableObject
    {
        [Header("Sávgeometria")]
        [Tooltip("A párhuzamos sávok száma (math-horde.md 3.2: a feladatok 2-4 sávot használhatnak).")]
        [Range(2, 4)]
        [SerializeField] private int _laneCount = 3;

        [Tooltip("Egy sáv szélessége világegységben (X tengely).")]
        [SerializeField] private float _laneWidth = 2.2f;

        [Tooltip("A pálya hossza világegységben (Z tengely): a játékos-zónától az ellenség-érkezési zónáig.")]
        [SerializeField] private float _arenaLength = 24f;

        [Tooltip("A sávok közötti elválasztó csík szélessége (vizuális elkülönítés, math-horde.md 3.1).")]
        [SerializeField] private float _laneDividerWidth = 0.18f;

        [Header("Zónák (arányosan a pálya hosszához, 0-1)")]
        [Tooltip("Az ellenség-érkezési zóna mélysége a pálya távoli (felső/képernyő tetején látszódó) végén.")]
        [Range(0.05f, 0.4f)]
        [SerializeField] private float _spawnZoneDepthRatio = 0.18f;

        [Tooltip("A játékos-zóna mélysége a pálya közeli (alsó harmad) végén.")]
        [Range(0.05f, 0.4f)]
        [SerializeField] private float _playerZoneDepthRatio = 0.15f;

        [Header("Kamera (fix, harmadik személyű, portrait 9:16 — math-horde.md 3.2)")]
        [SerializeField] private float _cameraHeight = 7.5f;
        [SerializeField] private float _cameraBackOffset = 4.5f;
        [Range(20f, 80f)]
        [SerializeField] private float _cameraFieldOfView = 55f;
        [Tooltip("Hova néz a kamera a pálya hosszán (0-1 arány) - ez tartja a spawn-zónát felül, a játékost alul.")]
        [Range(0.3f, 0.9f)]
        [SerializeField] private float _cameraLookAtDepthRatio = 0.55f;
        [SerializeField] private float _cameraLookAtHeight = 0.5f;

        [Header("Világítás")]
        [SerializeField] private Vector3 _sunRotationEuler = new Vector3(55f, -35f, 0f);
        [SerializeField] private float _sunIntensity = 1.15f;
        [SerializeField] private Color _sunColor = new Color(1f, 0.96f, 0.88f);

        public int LaneCount => Mathf.Clamp(_laneCount, 2, 4);
        public float LaneWidth => Mathf.Max(0.5f, _laneWidth);
        public float ArenaLength => Mathf.Max(4f, _arenaLength);
        public float LaneDividerWidth => Mathf.Max(0f, _laneDividerWidth);
        public float SpawnZoneDepthRatio => Mathf.Clamp(_spawnZoneDepthRatio, 0.05f, 0.4f);
        public float PlayerZoneDepthRatio => Mathf.Clamp(_playerZoneDepthRatio, 0.05f, 0.4f);
        public float CameraHeight => _cameraHeight;
        public float CameraBackOffset => _cameraBackOffset;
        public float CameraFieldOfView => _cameraFieldOfView;
        public float CameraLookAtDepthRatio => Mathf.Clamp(_cameraLookAtDepthRatio, 0.3f, 0.9f);
        public float CameraLookAtHeight => _cameraLookAtHeight;
        public Vector3 SunRotationEuler => _sunRotationEuler;
        public float SunIntensity => _sunIntensity;
        public Color SunColor => _sunColor;

        /// <summary>A pálya teljes szélessége (minden sáv + a köztük lévő elválasztók).</summary>
        public float TotalWidth => LaneCount * LaneWidth + (LaneCount - 1) * LaneDividerWidth;

        private void OnValidate()
        {
            _laneCount = Mathf.Clamp(_laneCount, 2, 4);
            _laneWidth = Mathf.Max(0.5f, _laneWidth);
            _arenaLength = Mathf.Max(4f, _arenaLength);
            _laneDividerWidth = Mathf.Max(0f, _laneDividerWidth);
        }
    }
}
