// Simplex Noise for C#
// Copyright © Benjamin Ward 2019
// See LICENSE
// Simplex Noise implementation offering 1D, 2D, and 3D forms w/ values in the range of 0 to 255.
// Based on work by Heikki Törmälä (2012) and Stefan Gustavson (2006).

using System;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace SimplexNoise
{
    [BurstCompile]
    public static class Noise
    {
	    /// <summary> 3D -1 to 1 gradient noise function. Analagous to Perlin Noise. </summary>
	    [MethodImpl(512)] // aggressive optimization on supported runtimes
	    [BurstCompile]
	    public static float GradientNoise3D(float x, float y, float z, int seed = 0)
	    {
		    // see comments in GradientNoise()
		    int ix = x > 0 ? (int)x : (int)x - 1;
		    int iy = y > 0 ? (int)y : (int)y - 1;
		    int iz = z > 0 ? (int)z : (int)z - 1;
		    float fx = x - ix;
		    float fy = y - iy;
		    float fz = z - iz;

		    ix += seed * Const.SeedPrime;

		    ix += Const.Offset;
		    iy += Const.Offset;
		    iz += Const.Offset;
		    int p1 = ix * Const.XPrime1 + iy * Const.YPrime1 + iz * Const.ZPrime1;
		    int p2 = ix * Const.XPrime2 + iy * Const.YPrime2 + iz * Const.ZPrime2;
		    int llHash = p1 * p2;
		    int lrHash = (p1 + Const.XPrime1) * (p2 + Const.XPrime2);
		    int ulHash = (p1 + Const.YPrime1) * (p2 + Const.YPrime2);
		    int urHash = (p1 + Const.XPlusYPrime1) * (p2 + Const.XPlusYPrime2);
		    float zLowBlend = InterpolateGradients3D(llHash, lrHash, ulHash, urHash, fx, fy, fz);
		    llHash = (p1 + Const.ZPrime1) * (p2 + Const.ZPrime2);
		    lrHash = (p1 + Const.XPlusZPrime1) * (p2 + Const.XPlusZPrime2);
		    ulHash = (p1 + Const.YPlusZPrime1) * (p2 + Const.YPlusZPrime2);
		    urHash = (p1 + Const.XPlusYPlusZPrime1) * (p2 + Const.XPlusYPlusZPrime2);
		    float zHighBlend = InterpolateGradients3D(llHash, lrHash, ulHash, urHash, fx, fy, fz - 1);
		    float sz = fz * fz * (3 - 2 * fz);
		    return zLowBlend + (zHighBlend - zLowBlend) * sz;
	    }
	    
	    [BurstCompile]
	    [MethodImpl(MethodImplOptions.AggressiveInlining)]
	    static unsafe float InterpolateGradients3D(int llHash, int lrHash, int ulHash, int urHash, float fx, float fy, float fz)
	    {
		    // see comments in InterpolateGradients2D()
		    int xHash, yHash, zHash;
		    xHash = (llHash & Const.GradAndMask) | Const.GradOrMask;
		    yHash = xHash << Const.GradShift1;
		    zHash = xHash << Const.GradShift2;
		    float llGrad = fx * *(float*)&xHash + fy * *(float*)&yHash + fz * *(float*)&zHash; // dot-product
		    xHash = (lrHash & Const.GradAndMask) | Const.GradOrMask;
		    yHash = xHash << Const.GradShift1;
		    zHash = xHash << Const.GradShift2;
		    float lrGrad = (fx - 1) * *(float*)&xHash + fy * *(float*)&yHash + fz * *(float*)&zHash;
		    xHash = (ulHash & Const.GradAndMask) | Const.GradOrMask;
		    yHash = xHash << Const.GradShift1;
		    zHash = xHash << Const.GradShift2;
		    float ulGrad = fx * *(float*)&xHash + (fy - 1) * *(float*)&yHash + fz * *(float*)&zHash; // dot-product
		    xHash = (urHash & Const.GradAndMask) | Const.GradOrMask;
		    yHash = xHash << Const.GradShift1;
		    zHash = xHash << Const.GradShift2;
		    float urGrad = (fx - 1) * *(float*)&xHash + (fy - 1) * *(float*)&yHash + fz * *(float*)&zHash;
		    float sx = fx * fx * (3 - 2 * fx);
		    float sy = fy * fy * (3 - 2 * fy);
		    float lowerBlend = llGrad + (lrGrad - llGrad) * sx;
		    float upperBlend = ulGrad + (urGrad - ulGrad) * sx;
		    return lowerBlend + (upperBlend - lowerBlend) * sy;
	    }
	    
		/// <summary> 2D -1 to 1 gradient noise function. Analagous to Perlin Noise. </summary>
		[MethodImpl(512)] // aggressive optimization on supported runtimes
		[BurstCompile]
		public static float GradientNoise2D(float x, float y, int seed = 0)
		{
		    // see comments in GradientNoise3D()
		    int ix = x > 0 ? (int)x : (int)x - 1;
		    int iy = y > 0 ? (int)y : (int)y - 1;
		    float fx = x - ix;
		    float fy = y - iy;

		    ix += seed * Const.SeedPrime;

		    ix += Const.Offset;
		    iy += Const.Offset;
		    
		    // Hash the integer coordinates
		    int p1 = ix * Const.XPrime1 + iy * Const.YPrime1;
		    int p2 = ix * Const.XPrime2 + iy * Const.YPrime2;

		    // Get hashes for the 4 corners of the square
		    int llHash = p1 * p2;
		    int lrHash = (p1 + Const.XPrime1) * (p2 + Const.XPrime2);
		    int ulHash = (p1 + Const.YPrime1) * (p2 + Const.YPrime2);
		    int urHash = (p1 + Const.XPlusYPrime1) * (p2 + Const.XPlusYPrime2);

		    // Interpolate the gradients at the 4 corners
		    return InterpolateGradients2D(llHash, lrHash, ulHash, urHash, fx, fy);
		}

		[BurstCompile]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		static unsafe float InterpolateGradients2D(int llHash, int lrHash, int ulHash, int urHash, float fx, float fy)
		{
		    // see comments in InterpolateGradients3D()
		    int xHash, yHash;
		    
		    // Lower-Left corner
		    xHash = (llHash & Const.GradAndMask) | Const.GradOrMask;
		    yHash = xHash << Const.GradShift1;
		    float llGrad = fx * *(float*)&xHash + fy * *(float*)&yHash; // 2D dot-product

		    // Lower-Right corner
		    xHash = (lrHash & Const.GradAndMask) | Const.GradOrMask;
		    yHash = xHash << Const.GradShift1;
		    float lrGrad = (fx - 1) * *(float*)&xHash + fy * *(float*)&yHash;

		    // Upper-Left corner
		    xHash = (ulHash & Const.GradAndMask) | Const.GradOrMask;
		    yHash = xHash << Const.GradShift1;
		    float ulGrad = fx * *(float*)&xHash + (fy - 1) * *(float*)&yHash;

		    // Upper-Right corner
		    xHash = (urHash & Const.GradAndMask) | Const.GradOrMask;
		    yHash = xHash << Const.GradShift1;
		    float urGrad = (fx - 1) * *(float*)&xHash + (fy - 1) * *(float*)&yHash;

		    // S-curve smoothing (quintic interpolation)
		    float sx = fx * fx * (3 - 2 * fx);
		    float sy = fy * fy * (3 - 2 * fy);

		    // Bilinear interpolation
		    float lowerBlend = llGrad + (lrGrad - llGrad) * sx;
		    float upperBlend = ulGrad + (urGrad - ulGrad) * sx;
		    return lowerBlend + (upperBlend - lowerBlend) * sy;
		}
		
		[BurstCompile]
		public static float WorleyNoise2D(float x, float y, int seed = 0)
		{
			int ix = x > 0 ? (int)x : (int)x - 1;
			int iy = y > 0 ? (int)y : (int)y - 1;
			float fx = x - ix;
			float fy = y - iy;

			float minDistanceSq = 4.0f; // Start with a value greater than any possible squared distance in a 1x1 cell

			// Check the 3x3 grid of cells around the current point
			for (int cy = iy - 1; cy <= iy + 1; cy++)
			{
				for (int cx = ix - 1; cx <= ix + 1; cx++)
				{
					// Get a deterministic, random feature point inside this cell
					int p1 = cx * Const.XPrime1 + cy * Const.YPrime1 + (seed * Const.SeedPrime);
					int p2 = cx * Const.XPrime2 + cy * Const.YPrime2;
					int hash = p1 * p2;

					// Use the hash to generate a stable but random-looking point within the cell (0-1 range)
					float pointX = (hash & 0xFFFF) * (1.0f / 65535.0f);
					float pointY = ((hash >> 16) & 0xFFFF) * (1.0f / 65535.0f);

					// Calculate the vector from the sample point to this feature point
					float dx = (cx + pointX) - x;
					float dy = (cy + pointY) - y;

					// Calculate the squared distance
					float distanceSq = dx * dx + dy * dy;

					// If this point is closer than any we've seen before, update the minimum distance
					if (distanceSq < minDistanceSq)
					{
						minDistanceSq = distanceSq;
					}
				}
			}

			// Return the distance to the nearest point. Sqrt is optional but gives a more linear result.
			return math.sqrt(minDistanceSq);
		}
		
	    [BurstCompile]
	    public static class Const
	    {
		    public const int FractalOctaves = 8;

		    internal const int
			    Offset = 0228125273,
			    SeedPrime = 525124619,
			    SeedMask = 0x0FFFFFFF,
			    XPrime1 = 0863909317,
			    YPrime1 = 1987438051,
			    ZPrime1 = 1774326877,
			    XPlusYPrime1 = unchecked(XPrime1 + YPrime1),
			    XPlusZPrime1 = unchecked(XPrime1 + ZPrime1),
			    YPlusZPrime1 = unchecked(YPrime1 + ZPrime1),
			    XPlusYPlusZPrime1 = unchecked(XPrime1 + YPrime1 + ZPrime1),
			    XMinusYPrime1 = unchecked(XPrime1 - YPrime1),
			    YMinusXPrime1 = unchecked(XPrime1 - YPrime1),
			    XPrime2 = 1299341299,
			    YPrime2 = 0580423463,
			    ZPrime2 = 0869819479,
			    XPlusYPrime2 = unchecked(XPrime2 + YPrime2),
			    XPlusZPrime2 = unchecked(XPrime2 + ZPrime2),
			    YPlusZPrime2 = unchecked(YPrime2 + ZPrime2),
			    XPlusYPlusZPrime2 = unchecked(XPrime2 + YPrime2 + ZPrime2),
			    XMinusYPrime2 = unchecked(XPrime2 - YPrime2),
			    YMinusXPrime2 = unchecked(XPrime2 - YPrime2),
			    GradAndMask = -0x7F9FE7F9, //-0x7F87F801
			    GradOrMask = 0x3F0FC3F0, //0x3F03F000
			    GradShift1 = 10,
			    GradShift2 = 20,
			    PeriodShift = 18,
			    WorleyAndMask = 0x007803FF,
			    WorleyOrMask = 0x3F81FC00,
			    PortionAndMask = 0x007FFFFF,
			    PortionOrMask = 0x3F800000;
	    }
    }
}