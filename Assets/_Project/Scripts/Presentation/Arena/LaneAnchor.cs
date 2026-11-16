// LaneAnchor.cs
// F1-12 — tisztán adat-jelölő komponens egy sávhoz, logika NÉLKÜL.
//
// A jövőbeli PlayerController (F1-13), EnemyPresenter (F1-14) és LanePresenter (F3-10)
// ebből olvassa ki, hol van az adott sáv X-középpontja, mekkora a szélessége és milyen
// hosszú a pálya mentén — így ezeknek a komponenseknek nem kell a vizuális primitívekhez
// (jelenlegi Plane/Cube geometriához) kötődniük, és a geometria később grafikai assetekre
// cserélhető a sávszámítás módosítása nélkül (math-horde.md 3.1: "a későbbi grafikai
// assetek cserélhetőek legyenek").
using UnityEngine;

namespace MathHorde.Presentation.Arena
{
    public sealed class LaneAnchor : MonoBehaviour
    {
        [SerializeField] private int _laneIndex;
        [SerializeField] private float _centerX;
        [SerializeField] private float _width;
        [SerializeField] private float _length;

        public int LaneIndex => _laneIndex;
        public float CenterX => _centerX;
        public float Width => _width;
        public float Length => _length;

        /// <summary>Csak adatbeállítás — nincs mellékhatás, nincs döntési logika.</summary>
        public void Initialize(int laneIndex, float centerX, float width, float length)
        {
            _laneIndex = laneIndex;
            _centerX = centerX;
            _width = width;
            _length = length;
        }
    }
}
