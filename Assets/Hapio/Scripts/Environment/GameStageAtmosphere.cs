using System;
using C6Lab;
using UnityEngine;

namespace Hapio
{
    /// <summary>Stage scenery only; follows the battle presentation clock, including pause.</summary>
    [DisallowMultipleComponent]
    public sealed class GameStageAtmosphere : MonoBehaviour
    {
        public enum Motion { Smoke, Flame, Ember, Cloud }
        [Serializable]
        public sealed class Layer
        {
            public Transform card;
            public Motion motion;
            public Vector3 position, scale;
            public Quaternion rotation = Quaternion.identity;
            public Color color = Color.white;
            public float opacity = .2f, duration = 7, rise = 2, drift = .5f, phase;
            [NonSerialized] public Renderer renderer;
            [NonSerialized] public MaterialPropertyBlock block;
        }
        public Layer[] layers = Array.Empty<Layer>();
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        void LateUpdate()
        {
            var session = GameSession.Instance;
            if (session == null || session.Network == null) return;
            Sample(session.Network.BattlePresentationTime);
        }

        public void Sample(double seconds)
        {
            foreach (var layer in layers)
            {
                if (layer == null || layer.card == null) continue;
                float time = (float)seconds;
                float cycle = Mathf.Repeat(time / Mathf.Max(.2f, layer.duration) + layer.phase, 1);
                Vector3 position = layer.position, scale = layer.scale;
                Quaternion rotation = layer.rotation;
                float alpha = layer.opacity;
                if (layer.motion == Motion.Cloud)
                {
                    var orbit = Quaternion.Euler(0, time * 360f / Mathf.Max(1, layer.duration), 0);
                    position = orbit * layer.position;
                    rotation = orbit * layer.rotation;
                    position.y += Mathf.Sin(time * .4f + layer.phase * 6.28f) * .12f;
                }
                else if (layer.motion == Motion.Flame)
                {
                    float flicker = Mathf.Sin(time * 3.1f + layer.phase * 13)
                        + .4f * Mathf.Sin(time * 5.7f + layer.phase * 23);
                    scale.x *= 1 - flicker * .035f;
                    scale.y *= 1 + flicker * .07f;
                    position.y += (scale.y - layer.scale.y) * .5f;
                    rotation *= Quaternion.Euler(0, 0, flicker * 2);
                    alpha *= .88f + flicker * .07f;
                }
                else
                {
                    float envelope = Mathf.SmoothStep(0, 1, Mathf.Clamp01(cycle / .2f))
                        * (1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01((cycle - .65f) / .35f)));
                    position.y += cycle * layer.rise;
                    position.x += layer.drift * cycle + Mathf.Sin(cycle * Mathf.PI * 2 + layer.phase * 7) * .12f;
                    float expansion = layer.motion == Motion.Smoke ? 1 + cycle * .35f : 1 - cycle * .3f;
                    scale *= expansion;
                    alpha *= envelope;
                }
                layer.card.localPosition = position;
                layer.card.localScale = scale;
                layer.card.localRotation = rotation;
                if (layer.renderer == null) layer.renderer = layer.card.GetComponent<Renderer>();
                if (layer.renderer == null) continue;
                if (layer.block == null) layer.block = new MaterialPropertyBlock();
                layer.renderer.GetPropertyBlock(layer.block);
                Color color = layer.color; color.a *= Mathf.Clamp01(alpha);
                layer.block.SetColor(BaseColor, color);
                layer.renderer.SetPropertyBlock(layer.block);
            }
        }

        void OnDisable()
        {
            foreach (var layer in layers)
            {
                if (layer == null || layer.card == null) continue;
                layer.card.localPosition = layer.position;
                layer.card.localScale = layer.scale;
                layer.card.localRotation = layer.rotation;
                var artwork = layer.card.GetComponent<LabEnvironmentCard>();
                if (artwork != null) artwork.Refresh();
            }
        }
    }
}
