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
        internal Vector3D AimPosition;
        internal Vector3D AimDirection;
        internal Vector3D AimUp = MatrixD.Identity.Up;
        internal MatrixD AimMatrix;
        internal double PointerAdjScale = 0.05f;
        internal double AdjScale;
        internal bool DrawReticle;
        internal uint ReticleOnSelfTick;
        internal uint MasterUpdateTick;
        internal int ReticleAgeOnSelf;
        internal readonly char FocusChar = "_"[0];
        internal Hud.TextureMap FocusTextureMap;


        private const string ShieldHudStr = "hud";
        private const string NoShieldHudStr = "hudnoshield";

        private readonly MyStringId _cross = MyStringId.GetOrCompute("TargetReticle");
        private readonly MyStringId _focus = MyStringId.GetOrCompute("DS_TargetFocus");
        private readonly MyStringId _focusSecondary = MyStringId.GetOrCompute("DS_TargetFocusSecondary");
        private readonly MyStringId _active = MyStringId.GetOrCompute("DS_ActiveTarget");
        private readonly Vector2 _targetDrawPosition = new Vector2(0, 0.25f);
        private readonly List<IHitInfo> _hitInfo = new List<IHitInfo>();
        private readonly Dictionary<MyEntity, GridAi.TargetInfo> _toPruneMasterDict = new Dictionary<MyEntity, GridAi.TargetInfo>(64);
        private readonly List<GridAi.TargetInfo> _toSortMasterList = new List<GridAi.TargetInfo>(64);
        private readonly List<MyEntity> _sortedMasterList = new List<MyEntity>(64);
        private readonly Dictionary<MyEntity, float> _masterTargets = new Dictionary<MyEntity, float>(64);
        private readonly Session _session;
        private Vector2 _pointerPosition = new Vector2(0, 0.0f);
        private Vector2 _3RdPersonPos = new Vector2(0, 0.0f);
        private Color _reticleColor = Color.White;
        private readonly Dictionary<string, IconInfo[]> _targetIcons = new Dictionary<string, IconInfo[]>()
        {
            {"size", new[] {
                new IconInfo(MyStringId.GetOrCompute("DS_TargetScout"), 0.125, new Vector2(0, 1.2f), -1, false),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetFighter"), 0.125, new Vector2(0, 1.2f), -1, false),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetFrigate"), 0.125, new Vector2(0, 1.2f), -1, false),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetDestroyer"), 0.125, new Vector2(0, 1.2f), -1, false),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetCruiser"), 0.125, new Vector2(0, 1.2f), -1, false),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetBattleCruiser"), 0.125, new Vector2(0, 1.2f), -1, false),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetCapital"), 0.125, new Vector2(0, 1.2f), -1, false),
            }},
            {"threat", new[] {
                new IconInfo(MyStringId.GetOrCompute("DS_TargetThreat1"), 0.0625, new Vector2(-0.18f, 1.0f), 0, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetThreat2"), 0.0625, new Vector2(-0.18f, 1.0f), 0, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetThreat3"), 0.0625, new Vector2(-0.18f, 1.0f), 0, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetThreat4"), 0.0625, new Vector2(-0.18f, 1.0f), 0, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetThreat5"), 0.0625, new Vector2(-0.18f, 1.0f), 0, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetThreat6"), 0.0625, new Vector2(-0.18f, 1.0f), 0, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetThreat7"), 0.0625, new Vector2(-0.18f, 1.0f), 0, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetThreat8"), 0.0625, new Vector2(-0.18f, 1.0f), 0, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetThreat9"), 0.0625, new Vector2(-0.18f, 1.0f), 0, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetThreat10"), 0.0625, new Vector2(-0.18f, 1.0f), 0, true),
            }},
            {"distance", new[] {
                new IconInfo(MyStringId.GetOrCompute("DS_TargetDistance10"), 0.0625, new Vector2(-0.10f, 1.0f), 1, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetDistance20"), 0.0625, new Vector2(-0.10f, 1.0f), 1, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetDistance30"), 0.0625, new Vector2(-0.10f, 1.0f), 1, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetDistance40"), 0.0625, new Vector2(-0.10f, 1.0f), 1, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetDistance50"), 0.0625, new Vector2(-0.10f, 1.0f), 1, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetDistance60"), 0.0625, new Vector2(-0.10f, 1.0f), 1, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetDistance70"), 0.0625, new Vector2(-0.10f, 1.0f), 1, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetDistance80"), 0.0625, new Vector2(-0.10f, 1.0f), 1, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetDistance90"), 0.0625, new Vector2(-0.10f, 1.0f), 1, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetDistance100"), 0.0625, new Vector2(-0.10f, 1.0f), 1, true),
            }},
            {"speed", new[] {
                new IconInfo(MyStringId.GetOrCompute("DS_TargetSpeed10"), 0.0625, new Vector2(-0.02f, 1.0f), 2, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetSpeed20"), 0.0625, new Vector2(-0.02f, 1.0f), 2, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetSpeed30"), 0.0625, new Vector2(-0.02f, 1.0f), 2, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetSpeed40"), 0.0625, new Vector2(-0.02f, 1.0f), 2, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetSpeed50"), 0.0625, new Vector2(-0.02f, 1.0f), 2, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetSpeed60"), 0.0625, new Vector2(-0.02f, 1.0f), 2, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetSpeed70"), 0.0625, new Vector2(-0.02f, 1.0f), 2, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetSpeed80"), 0.0625, new Vector2(-0.02f, 1.0f), 2, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetSpeed90"), 0.0625, new Vector2(-0.02f, 1.0f), 2, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetSpeed100"), 0.0625, new Vector2(-0.02f, 1.0f), 2, true),
            }},
            {"engagement", new[] {
                new IconInfo(MyStringId.GetOrCompute("DS_TargetIntercept"), 0.0625,  new Vector2(0.06f, 1.0f), 3, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetRetreat"), 0.0625, new Vector2(0.06f, 1.0f), 3, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetEngaged"), 0.0625, new Vector2(0.06f, 1.0f), 3, true),
            }},
            {"shield", new[] {
                new IconInfo(MyStringId.GetOrCompute("DS_TargetShield10"), 0.0625,  new Vector2(0.14f, 1.0f), 4, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetShield20"), 0.0625, new Vector2(0.14f, 1.0f), 4, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetShield30"), 0.0625, new Vector2(0.14f, 1.0f), 4, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetShield40"), 0.0625,  new Vector2(0.14f, 1.0f), 4, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetShield50"), 0.0625, new Vector2(0.14f, 1.0f), 4, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetShield60"), 0.0625, new Vector2(0.14f, 1.0f), 4, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetShield70"), 0.0625,  new Vector2(0.14f, 1.0f), 4, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetShield80"), 0.0625, new Vector2(0.14f, 1.0f), 4, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetShield90"), 0.0625, new Vector2(0.14f, 1.0f), 4, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetShield100"), 0.0625, new Vector2(0.14f, 1.0f), 4, true),
            }},
        };

        private readonly Dictionary<string, IconInfo[]> _targetHuds = new Dictionary<string, IconInfo[]>()
        {
            {"hud", new[] {
                new IconInfo(MyStringId.GetOrCompute("WC_HUD_ShieldPrimary"), 0.5f,  new Vector2(0.14f, .8f), 4, true),
            }},
            {"hudnoshield", new[] {
                new IconInfo(MyStringId.GetOrCompute("WC_HUD_NoShieldPrimary"), 0.5f,  new Vector2(0.14f, .8f), 4, true),
            }},
        };

        private readonly string[] _textValues = new[]
        {
            ""
        };

        private uint _cacheIdleTicks;
        private uint _lastDrawTick;

        private int _delay;
        private int _currentIdx;
        private int _endIdx = -1;
        private bool _cachedPointerPos;
        private bool _cachedTargetPos;
        private bool _3RdPersonDraw;

        internal TargetUi(Session session)
        {
            _session = session;
            var cm = session.HudUi.CharacterMap;
            Dictionary<char, Hud.TextureMap> monoText;
            if (cm.TryGetValue(Hud.FontType.Mono, out monoText))
            {
                FocusTextureMap = monoText[FocusChar];
            }
        }
    }
}
