// Setting class and enumerations for the motion blur effect

using System;
using UnityEngine;

namespace UnityStandardAssets.CinematicEffects
{
    public partial class MotionBlur : MonoBehaviour
    {
        /// Class used for storing settings of MotionBlur.
        [Serializable]
        public class Settings
        {
            /// The angle of rotary shutter. The larger the angle is, the longer
            /// the exposure time is. This value is only used in delta time mode.
            [SerializeField, Range(0, 360)]
            [Tooltip("The angle of rotary shutter. Larger values give longer exposure.")]
            public float shutterAngle;

            /// The amount of sample points, which affects quality and performance.
            [SerializeField]
            [Tooltip("The amount of sample points, which affects quality and performance.")]
            public int sampleCount;

            /// The maximum length of motion blur, given as a percentage of the
            /// screen height. The larger the value is, the stronger the effects
            /// are, but also the more noticeable artifacts it gets.
            [SerializeField, Range(0.5f, 10.0f)]
            [Tooltip("The maximum length of motion blur, given as a percentage " +
             "of the screen height. Larger values may introduce artifacts.")]
            public float maxBlurRadius;

            /// The strength of multi frame blending. The opacity of preceding
            /// frames are determined from this coefficient and time differences.
            [SerializeField, Range(0, 1)]
            [Tooltip("The strength of multi frame blending")]
            public float frameBlending;

            /// Returns the default settings.
            public static Settings defaultSettings
            {
                get
                {
                    return new Settings
                    {
                        shutterAngle = 270,
                        sampleCount = 10,
                        maxBlurRadius = 5,
                        frameBlending = 0
                    };
                }
            }
        }
    }
}
