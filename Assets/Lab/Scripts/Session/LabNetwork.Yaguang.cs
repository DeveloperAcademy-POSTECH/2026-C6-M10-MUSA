using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// 야광귀 섬광 해제 (#301): 눈부신 사람마다 박 위로 나온 야광귀 얼굴에 보이지 않는 판정 구를 둔다.
    /// 지귀 불(#272)과 같은 "주인이 정해진 판정" 경로(`LabJigwiFireSurface`)를 그대로 써서, 그 사람의 공만 얼굴에 맞고
    /// 다른 사람의 공은 통과해 박에 맞는다. 얼굴에 맞은 공은 `YaguangFaceFireId`로 구분해 Host가
    /// `LabModel.ConfirmFlashFaceHit`로 확정한다. 판정 구는 매 프레임 요괴 얼굴(렌더러 범위)을 따라간다.
    /// </summary>
    public sealed partial class LabNetwork
    {
        /// <summary>얼굴 판정의 fireId. 지귀 불의 fireId와 겹치지 않는다.</summary>
        public const string YaguangFaceFireId = "yaguang-face";
        private readonly Dictionary<ulong, LabJigwiFireSurface> yaguangFaceSurfaces = new Dictionary<ulong, LabJigwiFireSurface>();
        private Transform yaguangFaceRoot;
        private LabGourdYokai faceYokai;

        /// <summary>지금 얼굴 판정이 있는 사람 수 (테스트·디버그용)</summary>
        public int YaguangFaceTargetCount => yaguangFaceSurfaces.Count;

        private IEnumerable<LabJigwiFireSurface> YaguangFaceSurfacesFor(ulong player) =>
            yaguangFaceSurfaces.Values.Where(surface => surface != null && surface.OwnerPlayerId == player);

        /// <summary>
        /// 눈부신 사람마다 얼굴 판정 구를 하나씩 두고 요괴 얼굴 위치·크기에 맞춘다. 눈부신 사람이 없거나, 섬광 해제가
        /// 꺼져 있거나, 요괴 얼굴을 찾지 못하면 모두 치운다. 모든 기기에서 돈다(Client는 보이는 공이 얼굴에서 멈추게).
        /// </summary>
        private void SyncYaguangFaceSurfaces()
        {
            LabSnapshot snapshot = latest;
            if (snapshot == null || snapshot.phase != LabPhase.Playing || target == null || config == null
                || !config.YaguangFlashFaceRelease || snapshot.flashCount == 0)
            {
                ClearYaguangFaceSurfaces();
                return;
            }
            double now = IsHost ? battleClock.Now(Time.realtimeSinceStartupAsDouble) : snapshot.serverTime;
            var glared = new HashSet<ulong>();
            foreach (LabPlayerState player in snapshot.players ?? new LabPlayerState[0])
                if (player != null && LabYaguangFlash.Glared(snapshot, now, player.id, config.YaguangFlashGlareSeconds))
                    glared.Add(player.id);
            if (glared.Count == 0)
            {
                ClearYaguangFaceSurfaces();
                return;
            }
            if (faceYokai == null || !faceYokai.isActiveAndEnabled || !faceYokai.transform.IsChildOf(target.transform))
                faceYokai = target.GetComponentInChildren<LabGourdYokai>();
            if (faceYokai == null || !faceYokai.TryGetFace(out Vector3 center, out float faceRadius))
            {
                ClearYaguangFaceSurfaces();
                return;
            }
            float radius = Mathf.Max(.05f, faceRadius * config.YaguangFlashFaceSize);
            foreach (ulong player in glared)
            {
                if (!yaguangFaceSurfaces.TryGetValue(player, out var surface) || surface == null)
                {
                    if (yaguangFaceRoot == null)
                    {
                        var root = new GameObject("Yaguang Face Targets");
                        root.transform.SetParent(transform, false);
                        yaguangFaceRoot = root.transform;
                    }
                    surface = LabJigwiFireSurface.Create(yaguangFaceRoot, player, YaguangFaceFireId, "face", center, radius, false);
                    yaguangFaceSurfaces[player] = surface;
                }
                else surface.Configure(center, radius, true, false);
            }
            foreach (ulong player in yaguangFaceSurfaces.Keys.Where(id => !glared.Contains(id)).ToArray())
            {
                var surface = yaguangFaceSurfaces[player];
                if (surface != null) { surface.gameObject.SetActive(false); Destroy(surface.gameObject); }
                yaguangFaceSurfaces.Remove(player);
            }
        }

        private void ClearYaguangFaceSurfaces()
        {
            if (yaguangFaceSurfaces.Count == 0 && yaguangFaceRoot == null) return;
            yaguangFaceSurfaces.Clear();
            if (yaguangFaceRoot == null) return;
            yaguangFaceRoot.gameObject.SetActive(false);
            Destroy(yaguangFaceRoot.gameObject);
            yaguangFaceRoot = null;
        }

        /// <summary>
        /// Host: 공이 얼굴 판정에 맞았다. 던진 사람이 아직 눈부시면 그 사람 눈부심만 풀고 새 스냅샷을 바로 보낸다.
        /// 그 사이 눈부심이 끝났으면 빗나감으로 치되 던진 공 수에는 넣지 않는다(지귀 불과 같은 규칙).
        /// </summary>
        private void ResolveYaguangFaceHit(LabProjectileResult result, double now)
        {
            if (model.ConfirmFlashFaceHit(result.ProjectileId, now)) PublishSnapshot(true);
            else model.ConfirmMiss(result.ProjectileId, now, countThrow: false);
        }
    }
}
