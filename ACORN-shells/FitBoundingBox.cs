using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Rhino.Geometry;
using System.Linq;
using Rhino.Geometry.Collections;
using Rhino.Collections;
using GH.MiscToolbox.Components.Utilities;

namespace ACORN_shells
{
    /// <summary>
    /// Fits a bounding box to a shell segment, minimizing volume.
    /// </summary>
    public class FitBoundingBox : GH_Component
    {
        public FitBoundingBox()
          : base("Fit Bounding Box", "A:FitBBox",
              "Fits a bounding box to a shell segment, minimizing volume.",
              "ACORN", "Pinbed")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddBrepParameter("Segment", "S", "Shell segment. Must be Brep.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Angular resolution", "R", "Rotation angle for testing bounding boxes (in degrees). Smaller takes longer.", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Rotate 3D", "3D", "Allow 3D rotation of Bounding Box for minimum height", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddBoxParameter("MinBBox", "B", "Fitted Bounding Box", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Brep segment = null;
            double resolution = 10;
            bool rot3D = false;

            if (!DA.GetData(0, ref segment)) return;
            if (!DA.GetData(1, ref resolution)) return;
            if (!DA.GetData(2, ref rot3D)) return;


            // --- Fit bounding box to segment

            List<GeometryBase> segmentList = new List<GeometryBase>() { segment };
            System.Object minBBoxObj;

            /*
            // RIL's algorithm - just in case...
            //var rotations = 300;
            //var ierations = 1;
            //MinimumBoundingBox.RunScript(segment, null, rotations, iterations, ref minBBoxObj);
            */

            if (rot3D) minBBoxObj = PsarrasBoundingBox.Solve(segmentList, resolution, true, true, true);
            else minBBoxObj = PsarrasBoundingBox.Solve(segmentList, resolution, false, false, true);

            Box minBBox = (Box)minBBoxObj;



            // --- Correct bounding box orientation
            // --- Assign box plane to lowest of large faces, since used algorithm uses arbitrary orientation

            // get the two largest faces            
            Brep boxBrep = minBBox.ToBrep();
            //BrepFaceList boxFaces = boxBrep.Faces;

            // converting to RhinoList allows using Sort
            RhinoList<BrepFace> boxFaces = new RhinoList<BrepFace>(boxBrep.Faces);

            // get face areas to sort - a map function would be nice
            List<double> faceAreas = new List<double>();
            foreach (BrepFace boxFace in boxFaces) faceAreas.Add(AreaMassProperties.Compute(boxFace).Area);       
            boxFaces.Sort(faceAreas.ToArray());
            boxFaces.Reverse(); // sort is always ascending...
            RhinoList<BrepFace> largeFaces = boxFaces.GetRange(0, 2);

            // select face with centroid with lowest Z coord 
            List<double> faceZs = new List<double>();
            foreach (BrepFace largeFace in largeFaces) faceZs.Add(AreaMassProperties.Compute(largeFace).Centroid.Z);
            largeFaces.Sort(faceZs.ToArray());
            BrepFace bottomFace = boxFaces[0];

            // recalculate bounding box aligned with plane from bottom face
            bottomFace.TryGetPlane(out Plane bottomPlane);
            // certify that plane normal faces upward (normal.Z is positive)
            if (bottomPlane.Normal.Z < 0) bottomPlane.Flip();

            Box corrMinBBox = new Box(bottomPlane, segment);


            DA.SetData(0, corrMinBBox);
        }

        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                return ACORN_shells.Properties.Resources.ACORN_24;
            }
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("56566329-9ed6-43c9-8bf3-4d08a1790e0b"); }
        }
    }
}