// Setting class and enumerations for the motion blur effect

using System;
using UnityEngine;

namespace UnityStandardAssets.CinematicEffects
{
    public partial class MotionBlur : MonoBehaviour
    {
        /// How the exposure time is determined.
        public enum ExposureTime {
            /// Use Time.deltaTime as the exposure time.
            DeltaTime,
            /// Use a constant time given to shutterSpeed.
            Constant
        }

        /// Amount of sample points.
        public enum SampleCount {
            /// The minimum amount of samples.
            Low,
            /// A medium amount of samples. Recommended for typical use.
            Medium,
            /// A large amount of samples.
            High,
            /// Use a given number of samples (customSampleCount)
            Custom
        }

        /// Class used for storing settings of MotionBlur.
        [Serializable]
        public class Settings
        {
            /// How the exposure time is determined.
            [SerializeField]
            [Tooltip("How the exposure time is determined.")]
            public ExposureTime exposureTime;

            /// The angle of rotary shutter. The larger the angle is, the longer
            /// the exposure time is. This value is only used in delta time mode.
            [SerializeField, Range(0, 360)]
            [Tooltip("The angle of rotary shutter. Larger values give longer exposure.")]
            public float shutterAngle;

            /// The denominator of the custom shutter speed. This value is only
            /// used in constant time mode.
            [SerializeField]
            [Tooltip("The denominator of the shutter speed.")]
            public int shutterSpeed;

            /// The amount of sample points, which affects quality and performance.
            [SerializeField]
            [Tooltip("The amount of sample points, which affects quality and performance.")]
            public SampleCount sampleCount;

            /// The number of sample points. This value is only used when
            /// SampleCount.Custom is given to sampleCount.
            [SerializeField]
            public int customSampleCount;

            /// The maximum length of motion blur, given as a percentage of the
            /// screen height. The larger the value is, the stronger the effects
            /// are, but also the more noticeable artifacts it gets.
            [SerializeField, Range(0.5f, 10.0f)]
            [Tooltip("The maximum length of motion blur, given as a percentage " +
             "of the screen height. Larger values may introduce artifacts.")]
            public float maxBlurRadius;

            /// Returns the default settings.
            public static Settings defaultSettings
            {
                get
                {
                    return new Settings
                    {
                        exposureTime = ExposureTime.DeltaTime,
                        shutterAngle = 360,
                        shutterSpeed = 48,
                        sampleCount = SampleCount.Medium,
                        customSampleCount = 10,
                        maxBlurRadius = 5
                    };
                }
            }
        }
    }
}
