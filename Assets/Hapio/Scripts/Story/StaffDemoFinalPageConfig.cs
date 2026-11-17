using System;
using UnityEngine;

namespace Hapio
{
    /// <summary>Presentation-only, replaceable completion page data. No score or connection settings live here.</summary>
    [CreateAssetMenu(menuName = "Hapio/Demo/Final Page", fileName = "FinalPageConfig")]
    public sealed class StaffDemoFinalPageConfig : ScriptableObject
    {
        public const string ResourcePath = "Hapio/Demo/Staff/FinalPageConfig";
        public const string DefaultHeading = "함께 봉인해주어 고맙소!";
        public const string DefaultMessage = "아직 남은 요괴들이 있소.";
        public const string DefaultQrCaption = "앱스토어에서 만나보시오.";
        public const string DefaultReturnCaption = "수장고로 돌아가기";
        public string heading = DefaultHeading;
        [TextArea] public string message = DefaultMessage;
        [Tooltip("Replace with the final QR image. Empty uses an explicitly labelled placeholder, never a fake code.")]
        public Sprite qrSprite;
        [Tooltip("Document the destination encoded in qrSprite. The app does not manufacture a QR from this text.")]
        public string destinationUrl;
        public string qrCaption = DefaultQrCaption;
        public string returnCaption = DefaultReturnCaption;

        public bool HasDestination => Uri.TryCreate(destinationUrl, UriKind.Absolute, out Uri destination)
            && (destination.Scheme == Uri.UriSchemeHttps || destination.Scheme == Uri.UriSchemeHttp);
    }
}
