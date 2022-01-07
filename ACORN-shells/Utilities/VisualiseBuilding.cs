using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

using Rhino;
using Rhino.Collections;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;

using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace ACORN
{
    /// <summary>
    /// Generates demo building geometry
    /// </summary>
    public class VisualiseBuilding : GH_Component
    {
        public VisualiseBuilding()
          : base("Visualise Building", "VizBldg",
              "Generates whole building geometry, beyond the floor shells",
              "ACORN Shells", " Utilities")
        // adding spaces to category names as per https://www.grasshopper3d.com/forum/topics/change-order-of-plugin-sub-category-c 
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("BayGeometry", "BG", "Bay geometry", GH_ParamAccess.item);
            pManager.AddBrepParameter("Segments", "S", "Segment surfaces", GH_ParamAccess.list);
            pManager.AddNumberParameter("Thickness", "T", "Shell thickness", GH_ParamAccess.item);
            pManager.AddNumberParameter("CornerRadius", "CR", "Corner radius", GH_ParamAccess.item);
            pManager.AddNumberParameter("ColumnHeight", "CH", "Column height", GH_ParamAccess.item);
            pManager.AddBooleanParameter("FilletEdges", "FE", "Fillet edges for visualisation purposes. Computationally intensive, allow 20 seconds to calculate", GH_ParamAccess.item);
            pManager.AddNumberParameter("TieRodRadius", "TR", "Tie rod radius. Optional, default is thickness/2", GH_ParamAccess.item);
            pManager.AddIntegerParameter("NrSpacers", "NS", "Number of spacers. Optional, default is 10", GH_ParamAccess.item);

            pManager[6].Optional = true;
            pManager[7].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddBrepParameter("ThickSegments", "TS", "Thick shell segments", GH_ParamAccess.list);
            pManager.AddBrepParameter("Floor", "F", "Floor", GH_ParamAccess.item);
            pManager.AddBrepParameter("Spacers", "S", "Spacers", GH_ParamAccess.list);
            pManager.AddBrepParameter("Columns", "C", "Columns", GH_ParamAccess.list);
            pManager.AddBrepParameter("ColumnHeads", "CH", "Column heads", GH_ParamAccess.list);
            pManager.AddBrepParameter("TieRods", "TR", "Tie rods", GH_ParamAccess.item);
            pManager.AddBrepParameter("Braces", "B", "Braces", GH_ParamAccess.tree);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Curve bayGeometryInput = null;
            List<Brep> segments = new List<Brep>();
            double thickness = 0;
            double cornerRadius = 0;
            double columnHeight = 0;
            bool filletEdges = false;
            double tieRodRadius = 0;
            int nrPts = 10;

            if (!DA.GetData(0, ref bayGeometryInput)) return;
            if (!DA.GetDataList(1, segments)) return;
            if (!DA.GetData(2, ref thickness)) return;
            if (!DA.GetData(3, ref cornerRadius)) return;
            if (!DA.GetData(4, ref columnHeight)) return;
            if (!DA.GetData(5, ref filletEdges)) return;
            DA.GetData(6, ref tieRodRadius);
            DA.GetData(7, ref nrPts);

            // sets default tieRodRadius
            if (tieRodRadius == 0) tieRodRadius = thickness / 2;

            double tol = RhinoDoc.ActiveDoc.ModelAbsoluteTolerance;
            PolylineCurve bayPolylineCurve = (PolylineCurve) bayGeometryInput;
            Polyline bayGeometry = bayPolylineCurve.ToPolyline();

            // ----------- thicken segments ------------ //

            //bool filletEdges = false;

            double filletRadius = 0.02; // make input? // depend on thickness
            if (filletRadius > thickness / 3) filletRadius = thickness / 3; // ensure radius is smaller then thickness

            Curve path = new Line(Point3d.Origin, Vector3d.ZAxis, thickness).ToNurbsCurve();

            List<Brep> thickSegments = new List<Brep>();
            foreach (Brep segment in segments)
            {
                // add thickness upwards

                Brep thickSegment = segment.Faces[0].CreateExtrusion(path, true);

                if (filletEdges)
                {
                    // add fillet edges to segments (for viz)
                    int numberOfEdges = thickSegment.Edges.Count;
                    int[] edgeIndices = new int[numberOfEdges];
                    double[] startRadii = new double[numberOfEdges];
                    double[] endRadii = new double[numberOfEdges];

                    for (int i = 0; i < numberOfEdges; i++)
                    {
                        edgeIndices[i] = i;
                        startRadii[i] = filletRadius;
                        endRadii[i] = filletRadius;
                    }

                    var filletResult = Brep.CreateFilletEdges(
                      thickSegment,
                      edgeIndices,
                      startRadii,
                      endRadii,
                      BlendType.Fillet,
                      RailType.RollingBall,
                      tol);
                    if (filletResult.Count() != 0)
                        thickSegment = filletResult[0];


                }

                thickSegments.Add(thickSegment);
            }

            // -------------- flat floor -------------- //

            // extract original surface by untrimming one segment
            Brep singleSegment = segments[0];
            Surface shell = singleSegment.Faces[0].UnderlyingSurface();

            // get bounding box from segments
            BoundingBox segmentsUnionBox = new BoundingBox();
            foreach (var segment in thickSegments)
                segmentsUnionBox = BoundingBox.Union(segmentsUnionBox, segment.GetBoundingBox(false));

            // get topmost point (?)
            Point3d apex = segmentsUnionBox.PointAt(0.5, 0.5, 1.0);
            PolylineCurve bayCurve = (PolylineCurve)bayGeometryInput;
            PolylineCurve floorCrv = (PolylineCurve)bayCurve.Duplicate();
            double shellHeight = apex.Z;
            floorCrv.Translate(new Vector3d(0, 0, shellHeight));
            Brep floorSrf = Brep.CreatePlanarBreps(floorCrv, tol)[0];
            Brep floorBrep = floorSrf.Faces[0].CreateExtrusion(path, true);


            // -------------- floor spacers ---------------- //

            // make floorSpacers
            //List<Point3d> floorPts = new List<Point3d>();
            List<Brep> spacers = new List<Brep>();
            double ptDist = (double)1 / nrPts;

            for (double i = ptDist / 2; i < 1; i += ptDist)
                for (double j = ptDist / 2; j < 1; j += ptDist)
                {
                    Point3d pt = segmentsUnionBox.PointAt(i, j, 1);

                    // only project points inside bayGeometry
                    PointContainment ptIn = floorCrv.Contains(pt, Plane.WorldXY, tol);
                    if (ptIn == PointContainment.Inside)
                    {

                        Point3d[] result = Intersection.ProjectPointsToBreps(
                          new List<Brep> { shell.ToBrep() },
                          new List<Point3d> { pt },
                          new Vector3d(0, 0, -1),
                          tol);

                        if ((result != null) && (result.Length > 0)) { 
                            Point3d projPt = result[0];
                            Line axis = new Line(projPt, pt);

                            Brep spacer = Brep.CreatePipe(
                              axis.ToNurbsCurve(), thickness / 2, false, PipeCapMode.None, false, tol, tol)[0];
                            spacer.Translate(new Vector3d(0, 0, thickness / 2));
                            spacers.Add(spacer);
                        }
                    }
                }

            // ------------ make columns --------------- //
            //double columnHeight = 3;
            double columnHeadHeight = cornerRadius * 1.5;
            double columnRadius = cornerRadius / 2;
            List<Brep> columns = new List<Brep>();
            List<Brep> columnHeads = new List<Brep>();
            Point3dList corners = bayGeometry;
            corners.RemoveAt(0);
            foreach (Point3d corner in corners)
            {
                Line columnAxis = new Line(corner, new Vector3d(0, 0, columnHeight));
                Brep column = Brep.CreatePipe(columnAxis.ToNurbsCurve(), columnRadius, false, PipeCapMode.None, false, tol, tol)[0];
                column = column.CapPlanarHoles(tol);
                columns.Add(column);


                Curve topColumnHead = new Circle(corner, cornerRadius).ToNurbsCurve();
                Curve bottomColumnHead = new Circle(corner, columnRadius).ToNurbsCurve();
                bottomColumnHead.Translate(new Vector3d(0, 0, -columnHeadHeight));
                Brep columnHead = Brep.CreateFromLoft(
                  new List<Curve> { bottomColumnHead, topColumnHead },
                  Point3d.Unset, Point3d.Unset, LoftType.Straight, false)[0];
                columnHead = columnHead.CapPlanarHoles(tol);
                columnHeads.Add(columnHead);
            }

            // missing: translate all up; add tierods

            double freeHeight = columnHeight - shellHeight - thickness;


            // ------- make Tierods ----------- //

            PolylineCurve tierodCurve = (PolylineCurve)bayCurve.Duplicate();
            tierodCurve.Translate(new Vector3d(0, 0, -thickness));
            Brep tierods = Brep.CreatePipe(tierodCurve, tieRodRadius, false, PipeCapMode.None, false, tol, tol)[0];

            // ------- make X braces ---------- //
            List<Curve> braceCurves = tierodCurve.DuplicateSegments().ToList<Curve>();

            DataTree<Brep> braceRods = new DataTree<Brep>();
            double braceHeight = freeHeight - 0.300;
            int iPath = 0;
            foreach (Curve braceCurve in braceCurves)
            {
                GH_Path currPath = new GH_Path(iPath);

                Point3d startTop = braceCurve.PointAtStart;
                Point3d endTop = braceCurve.PointAtEnd;
                Point3d startBottom = startTop - Vector3d.ZAxis * braceHeight;
                Point3d endBottom = endTop - Vector3d.ZAxis * braceHeight;

                Curve braceDiag1 = new Line(startTop, endBottom).ToNurbsCurve();
                Brep braceRod1 = Brep.CreatePipe(braceDiag1, tieRodRadius, false, PipeCapMode.None, false, tol, tol)[0];
                braceRod1.Translate(0, 0, freeHeight);
                braceRods.Add(braceRod1, currPath);

                Curve braceDiag2 = new Line(startBottom, endTop).ToNurbsCurve();
                Brep braceRod2 = Brep.CreatePipe(braceDiag2, tieRodRadius, false, PipeCapMode.None, false, tol, tol)[0];
                braceRod2.Translate(0, 0, freeHeight);
                braceRods.Add(braceRod2, currPath);
                iPath ++;
            }


            // move everything up: thickSegments, floorBrep, spacers, columnHeads, tierods


            foreach (Brep brep in thickSegments)
                brep.Translate(0, 0, freeHeight);
            floorBrep.Translate(0, 0, freeHeight);
            foreach (Brep brep in spacers)
                brep.Translate(0, 0, freeHeight);
            foreach (Brep brep in columnHeads)
                brep.Translate(0, 0, freeHeight);
            tierods.Translate(0, 0, freeHeight);


            DA.SetDataList(0, thickSegments);
            DA.SetData(1, floorBrep);
            DA.SetDataList(2, spacers);
            DA.SetDataList(3, columns);
            DA.SetDataList(4, columnHeads);
            DA.SetData(5, tierods);
            DA.SetDataTree(6, braceRods);
        }

        public override GH_Exposure Exposure
        {
            get { return GH_Exposure.primary; }
        }


        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                // You can add image files to your project resources and access them like this:
                return ACORN_shells.Properties.Resources.vizBuilding;
            }
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("09b502b0-6794-4f57-87d2-500bdf1c3fd2"); }
        }

    }
}
