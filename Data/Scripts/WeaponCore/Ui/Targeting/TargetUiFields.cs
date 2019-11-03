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

        private readonly MyStringId _cross = MyStringId.GetOrCompute("Crosshair");
        private readonly List<IHitInfo> _hitInfo = new List<IHitInfo>();
        private readonly List<MyLineSegmentOverlapResult<MyEntity>> _pruneInfo = new List<MyLineSegmentOverlapResult<MyEntity>>();
        private Vector2 _pointerPosition = new Vector2(0, 0.25f);
        private Vector2 _3RdPersonPos = new Vector2(0, 0.25f);
        private readonly Vector2 _targetDrawPosition = new Vector2(0, 0.25f);
        private readonly Session _session;
        private readonly Dictionary<string, IconInfo[]> _targetIcons = new Dictionary<string, IconInfo[]>()
        {
            {"size", new[] {
                new IconInfo(MyStringId.GetOrCompute("DS_TargetCapital"), 0.1, new Vector2D(0, 1f), -1, false),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetCruiser"), 0.1, new Vector2D(0, 1f), -1, false),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetDestroyer"), 0.1, new Vector2D(0, 1f), -1, false),
            }},
            {"threat", new[] {
                new IconInfo(MyStringId.GetOrCompute("DS_TargetThreat1"), 0.05, new Vector2D(0, 0.85f), 0, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetThreat2"), 0.05, new Vector2D(0, 0.85f), 0, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetThreat3"), 0.05, new Vector2D(0, 0.85f), 0, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetThreat4"), 0.05, new Vector2D(0, 0.85f), 0, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetThreat5"), 0.05, new Vector2D(0, 0.85f), 0, true),
            }},
            {"distance", new[] {
                new IconInfo(MyStringId.GetOrCompute("DS_TargetDistanceNear"), 0.05, new Vector2D(0.05, 0.85f), 1, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetDistanceNearMid"), 0.05, new Vector2D(0.05, 0.85f), 1, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetDistanceFarMid"), 0.05, new Vector2D(0.05, 0.85f), 1, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetDistanceFar"), 0.05, new Vector2D(0.05, 0.85f), 1, true),
            }},
            {"speed", new[] {
                new IconInfo(MyStringId.GetOrCompute("DS_TargetSpeed10"), 0.05, new Vector2D(0.1, 0.85f), 2, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetSpeed20"), 0.05, new Vector2D(0.1, 0.85f), 2, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetSpeed30"), 0.05, new Vector2D(0.1, 0.85f), 2, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetSpeed40"), 0.05, new Vector2D(0.1, 0.85f), 2, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetSpeed50"), 0.05, new Vector2D(0.1, 0.85f), 2, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetSpeed60"), 0.05, new Vector2D(0.1, 0.85f), 2, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetSpeed70"), 0.05, new Vector2D(0.1, 0.85f), 2, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetSpeed80"), 0.05, new Vector2D(0.1, 0.85f), 2, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetSpeed90"), 0.05, new Vector2D(0.1, 0.85f), 2, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetSpeed100"), 0.05, new Vector2D(0.1, 0.85f), 2, true),
            }},
            {"engagement", new[] {
                new IconInfo(MyStringId.GetOrCompute("DS_TargetIntercept"), 0.05,  new Vector2D(0.15, 0.85f), 3, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetRetreat"), 0.05, new Vector2D(0.15, 0.85f), 3, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetEngaged"), 0.05, new Vector2D(0.15, 0.85f), 3, true),
            }},
            {"shield", new[] {
                new IconInfo(MyStringId.GetOrCompute("DS_TargetShieldLow"), 0.05,  new Vector2D(0.2, 0.85f), 4, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetShieldMed"), 0.05, new Vector2D(0.2, 0.85f), 4, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetShieldHigh"), 0.05, new Vector2D(0.2, 0.85f), 4, true),
            }},
        };

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
