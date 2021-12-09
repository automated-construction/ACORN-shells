using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// from original script
using System.Collections;

using Rhino;
using Rhino.Geometry;

using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace ACORN_shells
{
    static class MinimumBoundingBox
    {
        /* ==============================================*
        | Min Bounding Box Calculation                   |
        | Original Author Ilja Asanovic (Python)         |
        |                                                |
        | Parallel C# version, code refactored           |
        | RIL, R3                                        |
        ================================================= */

        // found in McNeel forum:
        // https://discourse.mcneel.com/t/minimum-oriented-bounding-box-implementation-in-grasshopper-python-script-node/64344/61

        public static void RunScript(GeometryBase G, System.Object P, int D, int I, ref object MinBB)
        {




            RhinoApp.ClearCommandHistoryWindow();

            var version = "R6";
            //Component.Message = "Parallel " + version; //only works as C# component?

            // --------------------------------------
            // INPUT
            // --------------------------------------
            var geo = (GeometryBase)G; // Geometry to align the bbox about
            m_rotations = D;            // Rotate Degrees. Default = 90º
            var iterations = I;

            // --------------------------------------
            // Modify the rotation step size to less than 1 (degree) if input
            // is greater than 180 rotation steps. This increases the number of
            // steps AND reduces the angle. Conversion from degrees to radians
            // is done in the respective functions since the angle is depending
            // on a integer loop-variable (i).
            // --------------------------------------
            m_rotation_factor = 1;
            if (m_rotations > 180)
            {
                // reduce whole degrees to a fraction of a degree
                m_rotation_factor = 180.0 / m_rotations;
                // increase the rotation steps needed to cover 180 degrees
                m_rotations = (int)(D / m_rotation_factor);
            }

            // --------------------------------------
            // Prepare initial plane
            // --------------------------------------
            // Start the rotation attempts from WorldXY plane, and then get to work
            var init_plane = Plane.WorldXY; // default
            System.Object obj = P;
            if (obj != null)
                init_plane = (Plane)obj; // custom user-defined start plane

            // --------------------------------------
            // Output
            // --------------------------------------
            var plane = init_plane;
            for (int i = 0; i < iterations; i++)
            {
                plane = RotateAllCombinationsOfPlane(geo, plane);
            }

            // --------------------------------------
            // Output
            // --------------------------------------
            var bb = Box.Unset; geo.GetBoundingBox(plane, out bb);
            MinBB = bb;

        }

        private static int m_rotations = 90; // default
        private static double m_rotation_factor;
        const double TO_RADIANS = Math.PI / 180;

        private static Plane RotateAllCombinationsOfPlane(GeometryBase geo, Plane initial_plane)
        {
            // --------------------------------------
            // Rotate about X axis of initial plane
            // --------------------------------------
            var rotation_axis = initial_plane.XAxis;
            var rotation = GetRotationAtMinimumVolume(geo, initial_plane, rotation_axis);
            var plane_x = initial_plane;
            plane_x.Rotate(rotation, rotation_axis);

            // --------------------------------------
            // Rotate about Y axis of rotated plane_x
            // --------------------------------------
            rotation_axis = plane_x.YAxis;
            rotation = GetRotationAtMinimumVolume(geo, plane_x, rotation_axis);
            var plane_y = plane_x;
            plane_y.Rotate(rotation, rotation_axis);

            // --------------------------------------
            // Rotate about Z axis of rotated plane_y
            // --------------------------------------
            rotation_axis = plane_y.ZAxis;
            rotation = GetRotationAtMinimumVolume(geo, plane_y, rotation_axis);
            var plane_z = plane_y;
            plane_z.Rotate(rotation, rotation_axis);

            // =====================================
            // Another round of rotations starting
            // from the 3 axes of the last plane_z
            // =====================================

            // --------------------------------------
            // Rotate about X axis of rotated plane_z
            // --------------------------------------
            rotation_axis = plane_z.XAxis;
            rotation = GetRotationAtMinimumVolume(geo, plane_z, rotation_axis);
            var plane_x_refined = plane_z;
            plane_x_refined.Rotate(rotation, rotation_axis);

            // --------------------------------------
            // Rotate about Y axis of refined plane_z
            // --------------------------------------
            rotation_axis = plane_x_refined.YAxis;
            rotation = GetRotationAtMinimumVolume(geo, plane_x_refined, rotation_axis);
            var plane_y_refined = plane_x_refined;
            plane_y_refined.Rotate(rotation, rotation_axis);

            // --------------------------------------
            // Rotate about Z axis of refined plane_y
            // --------------------------------------
            rotation_axis = plane_y_refined.ZAxis;
            rotation = GetRotationAtMinimumVolume(geo, plane_y_refined, rotation_axis);
            var plane_z_refined = plane_y_refined;
            plane_z_refined.Rotate(rotation, rotation_axis);

            return plane_z_refined; // the lastly rotated plane
        }

        /// <summary>
        /// Returns the rotation (in radians) of the smallest volume. The parallel loop
        /// performs m_rotations rotations of 1º each (90 rotations is default)
        /// </summary>
        private static double GetRotationAtMinimumVolume(GeometryBase geo, Plane start_plane, Vector3d rotation_axis)
        {
            var rotated_volumes = new double[m_rotations];
            var rad_angle_factor = m_rotation_factor * TO_RADIANS; // calculate once

            // Let the .NET platform take care of spreading out the for-loop iterations
            // on  different threads. To avoid data races we use local block variables
            // inside the for-loop, and results are put into array slots, each result into
            // its own index in the array, which prevents "collisions" (meaning, different
            // threads overwriting each others values).
            System.Threading.Tasks.Parallel.For(0, m_rotations, i =>
            {
                // Make a fresh new rotation starting from the original plane on each try.
                // A local variable (_plane) is required here in order to "isolate" it from
                // other threads operating simultaneously on other indexes (i), so they will
                // have their own copy of _plane, and therefore we don't overwrite each other.
                // Take this as  a general rule for Parallel.For-blocks
                var _plane = start_plane;

                // The rad_angle_factor is converting the integer ("degrees") into radians AND
                // transforms it into a fraction of a degree if the input (D) is greater than 180
                _plane.Rotate(i * rad_angle_factor, rotation_axis);

                // Since each thread works with different indexes (i), and when done, assigning
                // each result value into "its own array index", then no conflict can occur be-
                // tween threads so that any one thread would overwrite the result produced by
                // another thread, and therefore no data-races will occur. This is a red-neck
                // approach which ALWAYS works if the size of the array can be known in advance
                rotated_volumes[i] = geo.GetBoundingBox(_plane).Volume;
            });

            // now find that index (degree of rotation) at which we had the smallest BoundingBox
            var rotation = IndexOfMinimumVolume(ref rotated_volumes);
            // Convert Degrees to Radians before returning the angle
            return rotation * rad_angle_factor;
        }

        /// <summary>
        /// Returns the index of the smallest volume recorded in the array
        /// </summary>
        /// <returns>Returns the index of the smalles volume, which also is
        /// equal to the degree at which this volume was achieved.</returns>
        private static double IndexOfMinimumVolume(ref double[] recorded_volumes)
        {
            var min_index = -1;
            var min_value = Double.MaxValue;
            for (var i = 0; i < recorded_volumes.Length; i++)
            {
                if (recorded_volumes[i] < min_value)
                {
                    min_index = i;
                    min_value = recorded_volumes[i];
                }
            }
            return min_index; // Index is the same as the degree
        }


    }
}
