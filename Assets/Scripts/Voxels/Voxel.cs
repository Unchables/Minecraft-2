// ---- FILE: Voxel.cs ----

using UnityEngine; // Needed for Color32

namespace Voxels
{
    /// <summary>
    /// Defines the data for a single voxel, packed into a single 32-bit uint
    /// to provide a rich feature set while maintaining performance.
    /// Total size is 4 bytes.
    /// </summary>
    public struct Voxel
    {
        /// <summary>
        /// The single, raw data field for this voxel. Contains all data packed into 32 bits.
        /// </summary>
        public uint Data;

        // --- Constants for Bit Masking & Shifting ---
        private const int ID_BITS = 12;
        private const int WATER_BITS = 3;
        private const int ROTATION_BITS = 4;
        private const int TINT_BITS = 6;
        private const int STATE_BITS = 7;

        private const int WATER_SHIFT = ID_BITS;
        private const int ROTATION_SHIFT = ID_BITS + WATER_BITS;
        private const int TINT_SHIFT = ID_BITS + WATER_BITS + ROTATION_BITS;
        private const int STATE_SHIFT = ID_BITS + WATER_BITS + ROTATION_BITS + TINT_BITS;

        private const uint ID_MASK = (1u << ID_BITS) - 1;
        private const uint WATER_MASK = ((1u << WATER_BITS) - 1) << WATER_SHIFT;
        private const uint ROTATION_MASK = ((1u << ROTATION_BITS) - 1) << ROTATION_SHIFT;
        private const uint TINT_MASK = ((1u << TINT_BITS) - 1) << TINT_SHIFT;
        private const uint STATE_MASK = ((1u << STATE_BITS) - 1) << STATE_SHIFT;

        // --- Getters & Setters ---

        public ushort GetBlockID() => (ushort)(Data & ID_MASK);
        public void SetBlockID(ushort id) => Data = (Data & ~ID_MASK) | id;

        public byte GetWaterLevel() => (byte)((Data & WATER_MASK) >> WATER_SHIFT);
        public void SetWaterLevel(byte level) => Data = (Data & ~WATER_MASK) | ((uint)level << WATER_SHIFT);

        public byte GetRotation() => (byte)((Data & ROTATION_MASK) >> ROTATION_SHIFT);
        public void SetRotation(byte rot) => Data = (Data & ~ROTATION_MASK) | ((uint)rot << ROTATION_SHIFT);

        public byte GetStateFlags() => (byte)((Data & STATE_MASK) >> STATE_SHIFT);
        public void SetStateFlags(byte flags) => Data = (Data & ~STATE_MASK) | ((uint)flags << STATE_SHIFT);

        /// <summary>
        /// Gets the packed 6-bit tint value (R2G2B2).
        /// </summary>
        public byte GetTintColorPacked() => (byte)((Data & TINT_MASK) >> TINT_SHIFT);
        
        /// <summary>
        /// Sets the packed 6-bit tint value (R2G2B2).
        /// </summary>
        public void SetTintColorPacked(byte tint) => Data = (Data & ~TINT_MASK) | ((uint)tint << TINT_SHIFT);

        /// <summary>
        /// Decodes the 6-bit R2G2B2 tint into a standard Color32 object.
        /// This is useful for shaders or debugging.
        /// </summary>
        public Color32 GetTintColor()
        {
            byte packed = GetTintColorPacked();
            // Extract 2 bits for each channel
            byte r = (byte)((packed >> 4) & 0x03); // Bits 5,4
            byte g = (byte)((packed >> 2) & 0x03); // Bits 3,2
            byte b = (byte)(packed & 0x03);        // Bits 1,0

            // Scale 2-bit values (0-3) to full 8-bit range (0-255)
            // 0->0, 1->85, 2->170, 3->255
            return new Color32((byte)(r * 85), (byte)(g * 85), (byte)(b * 85), 255);
        }

        public bool IsSolid()
        {
            var id = GetBlockID();
            return id != AirID.Value;
        }
    }
}