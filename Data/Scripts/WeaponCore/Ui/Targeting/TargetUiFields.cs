using System.Collections.Generic;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;
using WeaponCore.Support;

namespace WeaponCore
{
    internal partial class TargetUi
    {
        internal Vector3D PointerOffset;
        internal Vector3D TargetOffset;
        internal double PointerAdjScale = 0.05f;
        internal double AdjScale;
        internal bool DrawReticle;

        private readonly MyStringId _cross = MyStringId.GetOrCompute("Crosshair");
        private readonly List<IHitInfo> _hitInfo = new List<IHitInfo>();
        private Vector2 _pointerPosition = new Vector2(0, 0.25f);
        private Vector2 _3RdPersonPos = new Vector2(0, 0.25f);
        private Color _reticleColor = Color.White;
        private readonly Vector2 _targetDrawPosition = new Vector2(0, 0.25f);
        private readonly Session _session;
        private readonly List<MyEntity> _targetCache = new List<MyEntity>();
        private readonly Dictionary<string, IconInfo[]> _targetIcons = new Dictionary<string, IconInfo[]>()
        {
            {"size", new[] {
                new IconInfo(MyStringId.GetOrCompute("DS_TargetScout"), 0.1, new Vector2D(0, 1.15f), -1, false),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetFighter"), 0.1, new Vector2D(0, 1.15f), -1, false),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetFrigate"), 0.1, new Vector2D(0, 1.15f), -1, false),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetDestroyer"), 0.1, new Vector2D(0, 1.15f), -1, false),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetCruiser"), 0.1, new Vector2D(0, 1.15f), -1, false),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetCapital"), 0.1, new Vector2D(0, 1.15f), -1, false),
            }},
            {"threat", new[] {
                new IconInfo(MyStringId.GetOrCompute("DS_TargetThreat1"), 0.05, new Vector2D(-0.15, 1f), 0, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetThreat2"), 0.05, new Vector2D(-0.15, 1f), 0, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetThreat3"), 0.05, new Vector2D(-0.15, 1f), 0, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetThreat4"), 0.05, new Vector2D(-0.15, 1f), 0, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetThreat5"), 0.05, new Vector2D(-0.15, 1f), 0, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetThreat6"), 0.05, new Vector2D(-0.15, 1f), 0, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetThreat7"), 0.05, new Vector2D(-0.15, 1f), 0, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetThreat8"), 0.05, new Vector2D(-0.15, 1f), 0, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetThreat9"), 0.05, new Vector2D(-0.15, 1f), 0, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetThreat10"), 0.05, new Vector2D(-0.15, 1f), 0, true),
            }},
            {"distance", new[] {
                new IconInfo(MyStringId.GetOrCompute("DS_TargetDistance10"), 0.05, new Vector2D(-0.10, 1f), 1, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetDistance20"), 0.05, new Vector2D(-0.10, 1f), 1, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetDistance30"), 0.05, new Vector2D(-0.10, 1f), 1, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetDistance40"), 0.05, new Vector2D(-0.10, 1f), 1, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetDistance50"), 0.05, new Vector2D(-0.10, 1f), 1, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetDistance60"), 0.05, new Vector2D(-0.10, 1f), 1, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetDistance70"), 0.05, new Vector2D(-0.10, 1f), 1, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetDistance80"), 0.05, new Vector2D(-0.10, 1f), 1, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetDistance90"), 0.05, new Vector2D(-0.10, 1f), 1, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetDistance100"), 0.05, new Vector2D(-0.10, 1f), 1, true),
            }},
            {"speed", new[] {
                new IconInfo(MyStringId.GetOrCompute("DS_TargetSpeed10"), 0.05, new Vector2D(-0.05, 1f), 2, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetSpeed20"), 0.05, new Vector2D(-0.05, 1f), 2, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetSpeed30"), 0.05, new Vector2D(-0.05, 1f), 2, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetSpeed40"), 0.05, new Vector2D(-0.05, 1f), 2, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetSpeed50"), 0.05, new Vector2D(-0.05, 1f), 2, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetSpeed60"), 0.05, new Vector2D(-0.05, 1f), 2, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetSpeed70"), 0.05, new Vector2D(-0.05, 1f), 2, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetSpeed80"), 0.05, new Vector2D(-0.05, 1f), 2, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetSpeed90"), 0.05, new Vector2D(-0.05, 1f), 2, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetSpeed100"), 0.05, new Vector2D(-0.05, 1f), 2, true),
            }},
            {"engagement", new[] {
                new IconInfo(MyStringId.GetOrCompute("DS_TargetIntercept"), 0.05,  new Vector2D(0.0, 1f), 3, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetRetreat"), 0.05, new Vector2D(0.0, 1f), 3, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetEngaged"), 0.05, new Vector2D(0.0, 1f), 3, true),
            }},
            {"shield", new[] {
                new IconInfo(MyStringId.GetOrCompute("DS_TargetShield10"), 0.05,  new Vector2D(0.05, 1f), 4, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetShield20"), 0.05, new Vector2D(0.05, 1f), 4, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetShield30"), 0.05, new Vector2D(0.05, 1f), 4, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetShield40"), 0.05,  new Vector2D(0.05, 1f), 4, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetShield50"), 0.05, new Vector2D(0.05, 1f), 4, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetShield60"), 0.05, new Vector2D(0.05, 1f), 4, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetShield70"), 0.05,  new Vector2D(0.05, 1f), 4, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetShield80"), 0.05, new Vector2D(0.05, 1f), 4, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetShield90"), 0.05, new Vector2D(0.05, 1f), 4, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetShield100"), 0.05, new Vector2D(0.05, 1f), 4, true),
            }},
        };

        private uint _cacheIdleTicks;

        private int _currentIdx;
        private int _endIdx = -1;
        private int _previousWheel;
        private int _currentWheel;
        private bool _cachedPointerPos;
        private bool _cachedTargetPos;
        private bool _altPressed;
        private bool _3RdPersonDraw;
        private bool _firstPerson;
        private bool _ctrlPressed;

        internal TargetUi(Session session)
        {
            _session = session;
        }
    }
}
