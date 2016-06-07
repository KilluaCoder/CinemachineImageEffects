// Setting class and enumerations for the motion blur effect

using System;
using UnityEngine;

namespace UnityStandardAssets.CinematicEffects
{
    public partial class MotionBlur : MonoBehaviour
    {
        /// How the exposure time (shutter speed) is determined.
        public enum ExposureMode
        {
            /// Constant time exposure (given by exposureTime).
            Constant,
            /// Frame rate dependent exposure. The exposure time is set to be
            /// deltaTime * exposureTimeScale.
            DeltaTime
        }

        /// Amount of sample points.
        public enum SampleCount
        {
            /// The minimum amount of samples.
            Low,
            /// A medium amount of samples. Recommended for typical use.
            Medium,
            /// A large amount of samples.
            High,
            /// Use a given number of samples (sampleCountValue).
            Variable
        }

        /// Class used for storing settings of MotionBlur.
        [Serializable]
        public class Settings
        {
            /// How the exposure time (shutter speed) is determined.
            [SerializeField]
            [Tooltip("How the exposure time (shutter speed) is determined.")]
            public ExposureMode exposureMode;

            /// The denominator of the shutter speed.
            /// This value is only used in the constant exposure mode.
            [SerializeField]
            [Tooltip("The denominator of the shutter speed.")]
            public int shutterSpeed;

            /// The scale factor to the exposure time.
            /// This value is only used in the delta time exposure mode.
            [SerializeField]
            [Tooltip("The scale factor to the exposure time.")]
            public float exposureTimeScale;

            /// The amount of sample points, which affects quality and performance.
            [SerializeField]
            [Tooltip("The amount of sample points, which affects quality and performance.")]
            public SampleCount sampleCount;

            /// The number of sample points. This value is only used when
            /// SampleCount.Variable is given to sampleCount.
            [SerializeField]
            public int sampleCountValue;

            /// The maximum length of blur trails, given as a percentage to the
            /// screen height. The larger the value is, the longer the trails are,
            /// but also the more noticeable artifacts it gets.
            [SerializeField, Range(0.5f, 10.0f)]
            [Tooltip("The maximum length of blur trails, specified as a percentage " +
             "to the screen height. Large values may introduce artifacts.")]
            public float maxBlurRadius;

            /// Returns the default settings.
            public static Settings defaultSettings
            {
                get
                {
                    return new Settings
                    {
                        exposureMode = ExposureMode.DeltaTime,
                        shutterSpeed = 30,
                        exposureTimeScale = 1,
                        sampleCount = SampleCount.Medium,
                        sampleCountValue = 12,
                        maxBlurRadius = 3.5f
                    };
                }
            }
        }
    }
}
