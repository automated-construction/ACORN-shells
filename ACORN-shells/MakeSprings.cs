using System;
using System.Drawing;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Display;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using System.Linq;
using Rhino.Geometry.Collections;
using Rhino.Collections;
using GH.MiscToolbox.Components.Utilities;
using Grasshopper;

namespace ACORN_shells
{
    /// <summary>
    /// Simulates the pinbed mould.
    /// </summary>
    public class MakeSprings : GH_Component
    {
        public MakeSprings()
          : base("Make Springs", "A:Springs",
              "Simulates the pinbed mould.",
              "ACORN", "Shells")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddBrepParameter("Shell segments", "SS", "Shell segments", GH_ParamAccess.list);
            pManager.AddNumberParameter("Spring distance", "D", "Target distance between springs", GH_ParamAccess.item);
            pManager.AddNumberParameter("Gap size", "G", "Distance between segments (optional, for visualisation)", GH_ParamAccess.item);

            pManager[2].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddBrepParameter("Offset segments", "OS", "Shell segments (selected is S is True)", GH_ParamAccess.tree);
            pManager.AddPointParameter("Segment spring locations", "SL", "Segment spring locations", GH_ParamAccess.tree);
            pManager.AddPointParameter("Edge spring locations", "EL", "Edge spring locations", GH_ParamAccess.tree);
            pManager.AddLineParameter("Spring lines", "L", "Pin axes", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Gap size", "G", "Distance between segments", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            double tol = Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance / 10; //smaller tollerance works!

            List<Brep> segments = new List<Brep>();
            double approxSpringDist = 0;
            double gapSize = 0.005; // optional, for viz

            if (!DA.GetDataList<Brep>(0, segments)) return;
            if (!DA.GetData(1, ref approxSpringDist)) return;
            DA.GetData(2, ref gapSize);

            //output trees
            DataTree<Brep> offsetSegments = new DataTree<Brep>();
            DataTree<Point3d> segmentSpringLocations = new DataTree<Point3d>();
            DataTree<Point3d> edgeSpringLocations = new DataTree<Point3d>();
            DataTree<Line> edgeSpringLines = new DataTree<Line>();

            // get spring locations by dividing segmented shell interface curves
            // make one Brep with all segments to work with topology
            Brep segmentedShell = Brep.JoinBreps(segments, tol)[0];


            // shatter all edges by approximate spring distance
            List<BrepEdge> segmentInterfaces = segmentedShell.Edges.ToList();

            for (int edgeIndex = 0; edgeIndex < segmentInterfaces.Count; edgeIndex++)
            //foreach (BrepEdge segmentInterface in segmentInterfaces)
            {
                GH_Path edgePath = new GH_Path(edgeIndex);
                BrepEdge segmentInterface = segmentedShell.Edges[edgeIndex];
                int springCount = (int)Math.Ceiling(segmentInterface.GetLength() / approxSpringDist);

                Point3d[] springs;
                segmentInterface.DivideByCount(springCount, true, out springs); // declare in out in VS
                edgeSpringLocations.AddRange(springs, edgePath);
            }



            // offset segments
            for (int faceIndex = 0; faceIndex < segments.Count; faceIndex++)
            //foreach (Brep segment in segments)
            {

                GH_Path segmentPath = new GH_Path(faceIndex);
                Brep segment = segments[faceIndex];

                // get offset curves per segment
                // try offset on surface for both directions (inwards and outwards), use the one that is not null (is on surface)
                List<Curve> offsetSurfEdges = new List<Curve>();
                BrepFace segmentFace = segment.Faces[0]; //offsetOnCurve only works on Surfaces and BrepFaces

                foreach (Curve edge in segment.Edges)
                {
                    Curve offsetEdge = null;
                    Curve[] offsetEdgeArray = edge.OffsetOnSurface(segmentFace, gapSize / 2, tol);
                    if (offsetEdgeArray != null)
                    {
                        offsetEdge = offsetEdgeArray[0];
                    }
                    else
                    {
                        offsetEdgeArray = edge.OffsetOnSurface(segmentFace, -gapSize / 2, tol);
                        if (offsetEdgeArray != null)
                        {
                            offsetEdge = offsetEdgeArray[0];
                        }
                        else
                        {
                            offsetEdge = edge;
                        }
                    }
                    offsetSurfEdges.Add(offsetEdge);
                }

                // split segmentFace with offset curves (using BrepFace.Split)
                Brep segmentFacePieces = segmentFace.Split(offsetSurfEdges, tol);
                // get brep's BrepFaces
                List<BrepFace> segmentFacePiecesFaces = segmentFacePieces.Faces.ToList<BrepFace>();

                // convert brepFaces to Breps - change to lambda map
                List<Brep> segmentFacePiecesFacesBreps = new List<Brep>();
                foreach (BrepFace face in segmentFacePiecesFaces)
                    segmentFacePiecesFacesBreps.Add(face.DuplicateFace(true));
                // order breps by area and pick largest
                Brep offsetSegment = segmentFacePiecesFacesBreps.OrderByDescending(o => o.GetArea()).ToList()[0];
                offsetSegments.Add(offsetSegment, segmentPath);




                // segment spring location for meshing, using Karamba meshing component (IPts)
                // get segments' point closest to spring locations
                // assuming order in segmentedShell.Faces is the same as input

                // retrieve spring locations for this face
                BrepFace segmentFaceTopo = segmentedShell.Faces[faceIndex];
                List<Point3d> faceSprings = new List<Point3d>();
                foreach (int edgeIndex in segmentFaceTopo.AdjacentEdges())
                    faceSprings.AddRange(edgeSpringLocations.Branch(edgeIndex));

                List<Point3d> offsetFaceSprings = new List<Point3d>(); // simplify in VS with map lambda functions?
                                                                       //offsetFaceSprings = offsetSegment.Faces[0].PullPointsToFace(faceSprings, gapSize * 10);
                foreach (Point3d faceSpring in faceSprings)
                {
                    Point3d offsetFaceSpring;
                    ComponentIndex ci; // for out in ClosestPoint; remove in VisualStudio
                    double s, t; // for out in ClosestPoint; remove in VisualStudio
                    Vector3d normal; // for out in ClosestPoint; remove in VisualStudio
                    offsetSegment.ClosestPoint(faceSpring, out offsetFaceSpring, out ci, out s, out t, gapSize * 2, out normal);
                    offsetFaceSprings.Add(offsetFaceSpring);
                }

                segmentSpringLocations.AddRange(offsetFaceSprings, segmentPath);
            }

            // get spring lines per edge - repeating actions from previous loop per segment? = make it a function
            // only for edges between two segments

            for (int edgeIndex = 0; edgeIndex < edgeSpringLocations.BranchCount; edgeIndex++)
            {
                GH_Path edgePath = new GH_Path(edgeIndex);
                List<Point3d> edgeSprings = edgeSpringLocations.Branch(edgeIndex);

                // get faces adjacent to current edge

                BrepEdge currEdge = segmentedShell.Edges[edgeIndex];
                int[] adjFaces = currEdge.AdjacentFaces();

                List<Brep> edgeSegments = new List<Brep>();
                foreach (int faceIndex in adjFaces)
                    // add offset segments
                    edgeSegments.Add(offsetSegments.Branch(faceIndex)[0]);
                //allEdgesSegments.AddRange(edgeSegments, edgePath); //TEST


                //iterate springs in current edge

                foreach (Point3d edgeSpring in edgeSprings)
                {
                    List<Point3d> springEnds = new List<Point3d>();

                    //iterate both faces adjacent to current edge - each edge has 2 adjacent faces, EXCEPT corners
                    foreach (Brep edgeSegment in edgeSegments)
                    {
                        Point3d springEnd;
                        ComponentIndex ci; // for out in ClosestPoint; remove in VisualStudio
                        double s, t; // for out in ClosestPoint; remove in VisualStudio
                        Vector3d normal; // for out in ClosestPoint; remove in VisualStudio
                        edgeSegment.ClosestPoint(edgeSpring, out springEnd, out ci, out s, out t, gapSize * 2, out normal);
                        springEnds.Add(springEnd);
                    }

                    // make spring line IF there are two spring ends
                    if (springEnds.Count == 2)
                        edgeSpringLines.Add(new Line(springEnds[0], springEnds[1]), edgePath);
                }

            }



            DA.SetDataTree(0, offsetSegments);
            DA.SetDataTree(1, segmentSpringLocations);
            DA.SetDataTree(2, edgeSpringLocations);
            DA.SetDataTree(3, edgeSpringLines);
            DA.SetData(4, gapSize);

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
            get { return new Guid("d48c042a-6742-4c63-8e20-3a38ae82404f"); }
        }
    }
}