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
    ///  OBSOLETE
    /// </summary>
    public class MakePinbedModules : GH_Component
    {
        public MakePinbedModules()
          : base("MakePinbedModules", "A:MakePinbedModules",
              "Creates 3D rectangules representing pinbed modules. Grouped by segment.",
              "ACORN", "Pinbed")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddBrepParameter("Segment", "S", "Shell segment. Must be Brep.", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Allow 3D?", "3D", "Allow 3D rotation of Bounding Box", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Psarras algorithm?", "P", "Use Psarras?", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddRectangleParameter("Modules", "M", "Rectangules representing pinbed modules.", GH_ParamAccess.item);
            // change to tree of rectangles
            pManager.AddNumberParameter("Efficiency", "E", "Area efficiency (segment area / occupied area in module)", GH_ParamAccess.item);
            
            // for tests
            pManager.AddBoxParameter("MinBBox", "B", "Minimum Bounding Box (in 3D)", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Brep segment = null;
            bool rot3D = false;
            bool psarras = false;

            if (!DA.GetData(0, ref segment)) return;
            if (!DA.GetData(1, ref rot3D)) return;
            if (!DA.GetData(2, ref rot3D)) return;

            var modules = Rectangle3d.Unset;
            var efficiency = 0;


            //------------------- CalculateModules cluster ------------------//

            // --- Fit bounding box to segment
            // --- fitBBox = : component in GH (COMPONENTIZE?)

            // expose parameters?
            var rotations = 300;
            var iterations = 1;

            // Convert geometry (trimmed surface) to mesh, because trimmed surface gets untrimmed for some reason
            // takes more time than using Mesh Brep in GH, WHY???
            var meshedSegment = Mesh.CreateFromBrep(segment, MeshingParameters.QualityRenderMesh)[0];

            // Calculate minimum bounding box
            // using RIL script
            System.Object minBBoxObj = null;
            MinimumBoundingBox.RunScript(segment, null, rotations, iterations, ref minBBoxObj);

            // using psarras algorithm
            List<GeometryBase> segmentList = new List<GeometryBase>() { segment };
            System.Object minBBoxObj2;
            //minBBoxObj2 = PsarrasBoundingBox.Solve(segmentList, 1, true, true, true);
            if (rot3D) minBBoxObj2 = PsarrasBoundingBox.Solve(segmentList, 1, true, true, true);
            else minBBoxObj2 = PsarrasBoundingBox.Solve(segmentList, 1, false, false, true);

            Box minBBox;
            if (psarras) minBBox = (Box)minBBoxObj2;
            else minBBox = (Box)minBBoxObj;


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
            //Plane bottomPlane = new Plane();
            bottomFace.TryGetPlane(out Plane bottomPlane);
            // certify that plane normal faces upward (normal.Z is positive)
            if (bottomPlane.Normal.Z < 0) bottomPlane.Flip();

            Box corrMinBBox = new Box(bottomPlane, segment);

            // end fitBBox




            /*
            // Project onto XY plane
            outline = Curve.ProjectToPlane(outline, Plane.WorldXY);

            // Explode to lines
            var explodedOutline = outline.DuplicateSegments();

            // Find centroid
            var areaMassProp = AreaMassProperties.Compute(outline);
            var centroid = areaMassProp.Centroid;

            // Trim edges to create plan edges
            var edges = explodedOutline.Select(l => l.Trim(CurveEnd.Both, cornerRadius)).ToList();

            // Create corners
            List<Curve> corners = new List<Curve>();

            for (int i = 0; i < edges.Count; i++)
            {
                var cornerPoint = explodedOutline[i].PointAtStart;
                var arcStart = explodedOutline[i].PointAtLength(cornerRadius);
                var prevIndex = i == 0 ? edges.Count - 1 : i - 1;
                var arcEnd = explodedOutline[prevIndex].PointAtLength(explodedOutline[prevIndex].GetLength() - cornerRadius);
                var toCentroid = new Vector3d(centroid - cornerPoint);
                toCentroid.Unitize();
                var arcInterior = cornerPoint + toCentroid * cornerRadius;
                var arc = new Arc(arcStart, arcInterior, arcEnd);
                corners.Add(new ArcCurve(arc));
            }

            // Join edges and corners
            var zippedCurves = corners.Zip(edges, (x, y) => new List<Curve>() { x, y }).SelectMany(x => x);
            var plan = Curve.JoinCurves(zippedCurves).FirstOrDefault();

            */

            DA.SetData(0, modules); //change to tree
            DA.SetData(1, efficiency);

            // testing
            DA.SetData(2, corrMinBBox); // for testing
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
            get { return new Guid("b9a459c4-e8a4-4e24-98e7-118023ef9b6e"); }
        }
    }
}