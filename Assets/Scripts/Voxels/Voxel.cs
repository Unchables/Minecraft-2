namespace Voxels
{
    /// <summary>
    /// Defines the data for a single voxel within a chunk's main data array.
    /// This struct is guaranteed to be an "unmanaged" type, containing only a single ushort.
    /// Its total size is 2 bytes.
    /// </summary>
    public struct Voxel
    {
        /// <summary>
        /// The single, raw data field for this voxel. It contains all data, packed into 16 bits.
        /// This is the ONLY field in the struct to ensure it remains unmanaged.
        /// </summary>
        public ushort Data;

        // We can define methods inside the struct to safely get and set the packed data
        // without affecting the struct's unmanaged status.

        /// <summary>
        /// Extracts the Block ID from the packed data.
        /// </summary>
        /// <returns>The 12-bit Block ID.</returns>
        public ushort GetBlockID()
        {
            // Shifts the 16 bits to the right by 4, removing the rotation data
            // and leaving the 12 bits of the ID.
            return (ushort)(Data >> 4);
        }

        /// <summary>
        /// Extracts the Rotation value from the packed data.
        /// </summary>
        /// <returns>The 4-bit rotation value.</returns>
        public byte GetRotation()
        {
            // Performs a bitwise AND with 15 (binary 0000 0000 0000 1111)
            // to isolate the lowest 4 bits.
            return (byte)(Data & 0x0F);
        }

        /// <summary>
        /// Sets the Block ID, preserving the existing Rotation value.
        /// </summary>
        /// <param name="blockID">The new 12-bit Block ID.</param>
        public void SetBlockID(ushort blockID)
        {
            // 1. (Data & 0x0F): Gets the current rotation value.
            // 2. (blockID << 4): Moves the new ID into the correct bit position.
            // 3. | (OR): Combines the new ID with the old rotation.
            Data = (ushort)((blockID << 4) | (Data & 0x0F));
        }

        /// <summary>
        /// Sets the Rotation, preserving the existing Block ID value.
        /// </summary>
        /// <param name="rotation">The new 4-bit rotation value.</param>
        public void SetRotation(byte rotation)
        {
            // 1. (Data & 0xFFF0): Gets the current Block ID (masking out the old rotation).
            // 2. (rotation & 0x0F): Ensures the new rotation is only 4 bits.
            // 3. | (OR): Combines the old ID with the new rotation.
            Data = (ushort)((Data & 0xFFF0) | (rotation & 0x0F));
        }
    }
}