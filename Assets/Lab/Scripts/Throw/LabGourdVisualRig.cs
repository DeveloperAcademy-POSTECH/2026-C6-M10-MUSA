using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// 박 모델 계약 (#507). 스테이지별 박 프리팹 루트에 붙어 칠·판정·요괴·줄 연결에 쓸 부분을 명시한다.
    /// 이 컴포넌트가 없는 프리팹(흰 박 `GourdPaint/WhiteGourd`)은 <see cref="LabGourdPaint"/>의 기존 경로를 그대로 탄다.
    /// 검사(<see cref="Validate"/>)에 실패한 계약은 흰 박으로 돌아간다. 같은 빌드의 모든 기기가 같은 결과를 내므로 판정이
    /// 기기마다 어긋나지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LabGourdVisualRig : MonoBehaviour
    {
        public const string PaintShaderName = "C6Lab/GourdPaint";

        [Tooltip("칠이 그려지는 몸통. 로컬 원점은 몸통 중심, +Y가 칠 극축이다(셰이더 normalize(positionOS)와 LabPaintSurface.CellFor가 같은 공간).")]
        public MeshRenderer paintShell;
        [Tooltip("같은 칠 공간을 쓰는 추가 렌더러(예: 뚜껑). 닫힌 자세 기준으로 구운 메시. 칠 셰이더 재질을 이미 가졌으면 그 재질을 유지하고, 아니면 paintMaterial을 받는다.")]
        public Renderer[] paintExtras = System.Array.Empty<Renderer>();
        [Tooltip("판정 껍데기. paintShell의 identity 자식 MeshFilter이며 렌더러가 없고 Read/Write가 켜져 있어야 한다.")]
        public MeshFilter hitShell;
        [Tooltip("C6Lab/GourdPaint 셰이더를 쓰는 칠 재질")]
        public Material paintMaterial;
        [Tooltip("줄 연결점. 없으면 판정 상단 중심.")]
        public Transform ropeSocket;
        [Tooltip("이 프리팹이 배율 1로 맞는 Gourd Visual Size 값(LabConfig). 흰 박은 1.8에 해당한다.")]
        [Min(.01f)] public float referenceVisualSize = 1.8f;
        [Tooltip("크기를 바꿀 때 제자리에 두는 점(프리팹 루트 기준). 없으면 루트 원점.")]
        public Transform sizeAnchor;
        [Tooltip("요괴 반응에 쓰는 legacy Animation. 없으면 요괴 반응 없음.")]
        public Animation yokaiAnimation;
        [Tooltip("요괴 클립의 시간표와 움찔 대상. 없으면 흰 박 시간표.")]
        public LabGourdYokaiProfile yokaiProfile;

        /// <summary>계약이 쓸 수 있는지. 실패 이유는 로그(`C6_GOURD_RIG_FALLBACK`)와 검사에 쓴다.</summary>
        public bool Validate(out string reason)
        {
            reason = Check();
            return reason == null;
        }

        private string Check()
        {
            if (!gameObject.activeSelf) return "The rig root is inactive.";
            if (paintShell == null || !paintShell.enabled || !paintShell.gameObject.activeSelf)
                return "paintShell must be an enabled, active MeshRenderer.";
            if (!paintShell.transform.IsChildOf(transform)) return "paintShell must be inside the rig.";
            var shellMesh = paintShell.GetComponent<MeshFilter>();
            if (shellMesh == null || shellMesh.sharedMesh == null) return "paintShell needs a mesh.";
            if (hitShell == null || hitShell.sharedMesh == null) return "hitShell needs a mesh.";
            if (hitShell.transform.parent != paintShell.transform) return "hitShell must be a direct child of paintShell.";
            if (hitShell.transform.localPosition != Vector3.zero || hitShell.transform.localRotation != Quaternion.identity
                || hitShell.transform.localScale != Vector3.one)
                return "hitShell must have an identity transform under paintShell.";
            if (hitShell.GetComponent<Renderer>() != null) return "hitShell must not render.";
            if (!hitShell.sharedMesh.isReadable) return "hitShell's mesh must be readable for the runtime collider.";
            if (!UsesPaintShader(paintMaterial)) return "paintMaterial must use " + PaintShaderName + ".";
            if (paintExtras != null)
                foreach (Renderer extra in paintExtras)
                    if (extra == null || !extra.transform.IsChildOf(transform) || extra == paintShell)
                        return "Every paint extra must be a separate renderer inside the rig.";
            if (ropeSocket != null && !ropeSocket.IsChildOf(transform)) return "ropeSocket must be inside the rig.";
            if (sizeAnchor != null && !sizeAnchor.IsChildOf(transform)) return "sizeAnchor must be inside the rig.";
            if (float.IsNaN(referenceVisualSize) || float.IsInfinity(referenceVisualSize) || referenceVisualSize < .01f)
                return "referenceVisualSize must be a positive number.";
            if (yokaiAnimation != null)
            {
                if (!yokaiAnimation.transform.IsChildOf(transform)) return "yokaiAnimation must be inside the rig.";
                if (yokaiAnimation.clip == null) return "yokaiAnimation needs a clip.";
                LabGourdYokaiProfile timeline = yokaiProfile != null ? yokaiProfile : LabGourdYokaiProfile.Legacy;
                if (!timeline.IsValid(out string profileReason)) return profileReason;
                if (yokaiAnimation.transform.Find(timeline.bodyPath) == null)
                    return "yokaiProfile.bodyPath is not under yokaiAnimation.";
            }
            return null;
        }

        /// <summary>칠 셰이더와 칠 지도 속성을 가진 재질인지. 그런 재질의 추가 렌더러는 자기 재질(바탕 그림)을 유지한다.</summary>
        public static bool UsesPaintShader(Material material) => material != null && material.shader != null
            && material.shader.name == PaintShaderName && material.HasProperty("_PaintMap")
            && material.HasProperty("_PaintMapReady") && material.HasProperty("_PainterMap");

        /// <summary>크기 고정점의 루트 기준 위치(루트 배율 1 기준).</summary>
        public Vector3 AnchorInRoot => sizeAnchor != null ? transform.InverseTransformPoint(sizeAnchor.position) : Vector3.zero;

        /// <summary>렌더러 칠 대상: 몸통과 추가 렌더러.</summary>
        public int PaintRendererCount => 1 + (paintExtras != null ? paintExtras.Length : 0);
    }
}
