using System;
using UnityEngine;

namespace Hapio
{
    /// <summary>Presentation-only, replaceable completion page data. No score or connection settings live here.</summary>
    [CreateAssetMenu(menuName = "Hapio/Demo/Final Page", fileName = "FinalPageConfig")]
    public sealed class StaffDemoFinalPageConfig : ScriptableObject
    {
        public const string ResourcePath = "Hapio/Demo/Staff/FinalPageConfig";
        public string heading = "함께 봉인해 주어 고맙소!";
        [TextArea] public string message = "합이오 시연을 마쳤소.\n다음 만남을 기다리겠소.";
        [Tooltip("Replace with the final QR image. Empty uses an explicitly labelled placeholder, never a fake code.")]
        public Sprite qrSprite;
        [Tooltip("Document the destination encoded in qrSprite. The app does not manufacture a QR from this text.")]
        public string destinationUrl;
        public string qrCaption = "QR 준비 중 · 추후 교체 예정";

        public bool HasDestination => Uri.TryCreate(destinationUrl, UriKind.Absolute, out Uri destination)
            && (destination.Scheme == Uri.UriSchemeHttps || destination.Scheme == Uri.UriSchemeHttp);
    }
}
