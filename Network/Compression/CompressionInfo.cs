using System;
using YSBCaptain.Core;
using YSBCaptain.Core.Logging;
using YSBCaptain.Extensions;

namespace YSBCaptain.Network.Compression
{
    /// <summary>
    /// Compression metadata and configuration.
    /// Follows the layered architecture:
    /// Core → Extensions → Network/Performance → Gameplay → Patches
    /// This component depends on Core and Extensions layers.
    /// </summary>
    public class CompressionInfo
    {
        /// <summary>
        /// Integer compression configuration
        /// </summary>
        public class Integer
        {
            public int MinValue { get; }
            public int MaxValue { get; }
            public bool AllowUnsigned { get; }

            public Integer(int minValue, int maxValue, bool allowUnsigned)
            {
                if (maxValue <= minValue)
                    throw new ArgumentException("MaxValue must be greater than MinValue");

                MinValue = minValue;
                MaxValue = maxValue;
                AllowUnsigned = allowUnsigned;
            }

            public int GetRequiredBits()
            {
                long range = (long)MaxValue - MinValue;
                return (int)Math.Ceiling(Math.Log(range + 1, 2));
            }
        }

        /// <summary>
        /// Float compression configuration
        /// </summary>
        public class Float
        {
            public float MinValue { get; }
            public float MaxValue { get; }
            public int Precision { get; }

            public Float(float minValue, float maxValue, int precision)
            {
                if (maxValue <= minValue)
                    throw new ArgumentException("MaxValue must be greater than MinValue");

                MinValue = minValue;
                MaxValue = maxValue;
                Precision = precision;
            }

            public int GetRequiredBits()
            {
                float range = MaxValue - MinValue;
                return (int)Math.Ceiling(Math.Log(range * Math.Pow(10, Precision) + 1, 2));
            }
        }

        /// <summary>
        /// Vector3 compression configuration
        /// </summary>
        public class Vector3
        {
            public Float X { get; }
            public Float Y { get; }
            public Float Z { get; }

            public Vector3(Float x, Float y, Float z)
            {
                X = x ?? throw new ArgumentNullException(nameof(x));
                Y = y ?? throw new ArgumentNullException(nameof(y));
                Z = z ?? throw new ArgumentNullException(nameof(z));
            }
        }

        // Common compression configurations
        public Integer AgentOffset { get; } = new Integer(0, 4096, true);
        public Float Position { get; } = new Float(-1000f, 1000f, 16);
        public Float Rotation { get; } = new Float(-180f, 180f, 12);
        public Vector3 Movement { get; } = new Vector3(
            new Float(-10f, 10f, 8),
            new Float(-10f, 10f, 8),
            new Float(-10f, 10f, 8)
        );
    }
}
