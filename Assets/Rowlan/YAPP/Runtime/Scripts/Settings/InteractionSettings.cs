using UnityEngine;
using static Rowlan.Yapp.PrefabSettings;

namespace Rowlan.Yapp
{
    [System.Serializable]
    public class InteractionSettings
    {
        public enum InteractionType
        {
            AntiGravity,
            Magnet,
            ChangeScale,
            SetScale,
            RandomTransform
        }

        [System.Serializable]
        public class AntiGravity
        {
            /// <summary>
            /// Anti Gravity strength from 0..100
            /// </summary>
            [Range(0, 100)]
            public int strength = 30;

        }

        [System.Serializable]
        public class Magnet
        {
            /// <summary>
            /// Some arbitrary magnet strength from 0..100
            /// </summary>
            [Range(0, 100)]
            public int strength = 10;

        }

        [System.Serializable]
        public class ChangeScale
        {
            /// <summary>
            /// Some arbitrary strength from 0..100
            /// </summary>
            [Range(0, 100)]
            public float changeScaleStrength = 10f;

        }

        [System.Serializable]
        public class SetScale
        {
            /// <summary>
            /// Some arbitrary strength from 0..10
            /// </summary>
            [Range(0, 10)]
            public float setScaleValue = 1f;

        }

        [System.Serializable]
        public class RandomTransform
        {
            /// <summary>
            /// Randomize rotation
            /// </summary>
            public bool randomRotation;

            /// <summary>
            /// The rotation range
            /// </summary>
            public RotationRange rotationRange = RotationRange.Base_360;

            /// <summary>
            /// Minimum X rotation in degrees when random rotation is used.
            /// </summary>
            public float rotationMinX = 0f;

            /// <summary>
            /// Maximum X rotation in degrees when random rotation is used.
            /// </summary>
            public float rotationMaxX = 360f;

            /// <summary>
            /// Minimum Y rotation in degrees when random rotation is used.
            /// </summary>
            public float rotationMinY = 0f;

            /// <summary>
            /// Maximum Y rotation in degrees when random rotation is used.
            /// </summary>
            public float rotationMaxY = 360f;

            /// <summary>
            /// Minimum Z rotation in degrees when random rotation is used.
            /// </summary>
            public float rotationMinZ = 0f;

            /// <summary>
            /// Maximum Z rotation in degrees when random rotation is used.
            /// </summary>
            public float rotationMaxZ = 360f;

            /// <summary>
            /// Randomize Scale Minimum
            /// </summary>
            public bool changeScale = true;

            /// <summary>
            /// Randomize Scale Minimum
            /// </summary>
            public float scaleMin = 0.5f;

            /// <summary>
            /// Randomize Scale Maximum
            /// </summary>
            public float scaleMax = 1.5f;

        }

        #region Public Editor Fields

        public InteractionType interactionType;

        public AntiGravity antiGravity = new AntiGravity();
        public Magnet magnet = new Magnet();
        public ChangeScale changeScale = new ChangeScale();
        public SetScale setScale = new SetScale();
        public RandomTransform randomTransform = new RandomTransform();

        #endregion Public Editor Fields



    }
}
