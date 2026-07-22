using System.Collections.Generic;
using Fusion;
using Photon.Voice.Fusion;
using Photon.Voice.Unity;
using UnityEngine;

namespace CampLantern.Networking.Voice
{
    /// <summary>
    /// 상대 플레이어별 원터치 음소거 (step-09). 상대 음성 아바타의 Speaker AudioSource를
    /// 로컬에서만 mute 한다 — 서버 개입 없음 (GDD 6-4 안전장치의 P0 최소판, 차단·신고는 소셜 MVP 이후).
    /// 바인딩은 VoiceController가 세션 시작 시 Push — 스스로 외부 상태를 조회하지 않는다 (rules/scripts.md).
    /// </summary>
    public class PlayerMute : MonoBehaviour
    {
        private readonly HashSet<PlayerRef> m_muted = new HashSet<PlayerRef>();
        private readonly List<VoiceNetworkObject> m_voiceObjectsBuffer = new List<VoiceNetworkObject>();

        private NetworkRunner m_runner;
        private VoiceConnection m_voice;

        /// <summary>
        /// 해당 플레이어가 음소거 상태인지. 음소거 목록은 이 컴포넌트 수명(=씬) 안에서만 유지된다 —
        /// PlayerRef가 세션(Photon Room) 스코프 ID라 공간 이동 후에는 같은 사람이라도 다른 값이 되므로
        /// 씬 간 보존은 불가능하며, 영속 음소거는 백엔드 userId 확정 후(소셜 MVP) 키를 바꿔 구현한다.
        /// </summary>
        public bool IsMuted(PlayerRef player) => m_muted.Contains(player);

        /// <summary>원터치 음소거 — 상대 스피커 로컬 차단. 스피커가 아직 없으면(늦은 접속) 링크 시점에 적용된다.</summary>
        public void SetMuted(PlayerRef player, bool muted)
        {
            if (muted) m_muted.Add(player);
            else m_muted.Remove(player);
            Apply(player, muted);
        }

        /// <summary>VoiceController가 세션 시작 시 호출. 새 스피커 링크를 구독하고 보관된 음소거 상태를 재적용한다.</summary>
        public void Bind(NetworkRunner runner, VoiceConnection voice)
        {
            Unbind();
            m_runner = runner;
            m_voice  = voice;
            m_voice.SpeakerLinked -= OnSpeakerLinked;
            m_voice.SpeakerLinked += OnSpeakerLinked;
            foreach (PlayerRef player in m_muted) Apply(player, true);
        }

        /// <summary>세션 종료 시 VoiceController가 호출.</summary>
        public void Unbind()
        {
            if (m_voice != null) m_voice.SpeakerLinked -= OnSpeakerLinked;
            m_voice  = null;
            m_runner = null;
        }

        private void OnDestroy()
        {
            Unbind();
        }

        private void Apply(PlayerRef player, bool muted)
        {
            if (m_runner == null) return;

            m_voiceObjectsBuffer.Clear();
            m_runner.GetAllBehaviours(m_voiceObjectsBuffer);
            foreach (VoiceNetworkObject voiceObject in m_voiceObjectsBuffer)
            {
                if (voiceObject.Object == null) continue; // despawn 중 방어
                if (voiceObject.Object.StateAuthority != player) continue; // Shared Mode — 스폰 주인이 State Authority
                MuteSpeaker(voiceObject.SpeakerInUse, muted);
            }
        }

        // 음소거 이후에 링크된 스피커(늦은 접속·재접속)에도 보관된 상태를 적용
        private void OnSpeakerLinked(Speaker speaker)
        {
            if (m_muted.Count == 0 || speaker == null) return;

            var voiceObject = speaker.GetComponentInParent<VoiceNetworkObject>();
            if (voiceObject == null || voiceObject.Object == null) return;
            if (m_muted.Contains(voiceObject.Object.StateAuthority))
                MuteSpeaker(speaker, true);
        }

        private static void MuteSpeaker(Speaker speaker, bool muted)
        {
            if (speaker == null) return;
            // Speaker는 [RequireComponent(typeof(AudioSource))] — 항상 같은 GO에 존재
            var source = speaker.GetComponent<AudioSource>();
            if (source != null) source.mute = muted;
        }
    }
}
