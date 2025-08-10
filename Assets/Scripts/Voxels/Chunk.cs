using System;
using Unity.Collections;

namespace Voxels
{
    public class Chunk : IDisposable
    {
        // this should be equal to 32
        public const int ChunkSize = 32;
        public const int ChunkSizeMinusOne = ChunkSize - 1;
        public const int xShift = 10;
        public const int yShift = 5;
        public const int zShift = 0;

        public NativeArray<Voxel> voxels;

        public Chunk()
        {
            voxels = new NativeArray<Voxel>(ChunkSize * ChunkSize * ChunkSize, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        }

        public void Dispose()
        {
            if(voxels.Length > 0)
                voxels.Dispose();
        }
    }
}