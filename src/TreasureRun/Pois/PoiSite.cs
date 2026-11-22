using TreasureRun.Core;
using UnityEngine;

namespace TreasureRun.Pois
{
    /// <summary>
    /// A placed Point of Interest: lives on the section's tile root. Subclasses add the POI's structure and behaviour
    /// in <see cref="OnPlaced"/>; their state goes into <see cref="Record"/> (plain data, for the planned save/resume).
    /// </summary>
    public abstract class PoiSite : MonoBehaviour
    {
        public PoiDef Def { get; private set; }
        public PoiRecord Record { get; private set; }
        public SectionLayout Layout { get; private set; }

        /// <summary>World position of the section's centre cell, at path height.</summary>
        public Vector3 Center => transform.position;

        /// <summary>World position of a section cell's centre (local cell coordinates 0..6) at a height in thirds.</summary>
        public Vector3 CellWorld(int x, int z, float heightThirds = 0f) =>
            transform.TransformPoint(new Vector3(x - SectionLayout.Half, heightThirds / 3f, z - SectionLayout.Half));

        internal void Init(PoiDef def, PoiRecord record, SectionLayout layout)
        {
            Def = def;
            Record = record;
            Layout = layout;
            OnPlaced();
        }

        protected abstract void OnPlaced();

        /// <summary>A short status line for the dev console.</summary>
        public virtual string Status => "";

        protected static bool Combat => SpawnManager.instance != null && SpawnManager.instance.combat;
        protected static bool Running => TreasureRunMode.Active && GameManager.instance != null && !GameManager.instance.gameOver;
    }
}
