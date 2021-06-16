using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

using Rhino;
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
          : base("Visualise Building", "A:VizBldg",
              "Generates demo building geometry",
              "ACORN", "Shells")
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

            pManager[6].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddBrepParameter("ThickSegments", "TS", "Thick shell segments", GH_ParamAccess.list);
            pManager.AddBrepParameter("Floor", "F", "Floor", GH_ParamAccess.item);
            pManager.AddBrepParameter("Spacers", "S", "Spacers", GH_ParamAccess.list);
            pManager.AddBrepParameter("Columns", "C", "Columns", GH_ParamAccess.list);
            pManager.AddBrepParameter("ColumnHeads", "CH", "Column heads", GH_ParamAccess.list);
            pManager.AddBrepParameter("TieRods", "TR", "Tie rods", GH_ParamAccess.item);
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

            if (!DA.GetData(0, ref bayGeometryInput)) return;
            if (!DA.GetDataList(1, segments)) return;
            if (!DA.GetData(2, ref thickness)) return;
            if (!DA.GetData(3, ref cornerRadius)) return;
            if (!DA.GetData(4, ref columnHeight)) return;
            if (!DA.GetData(5, ref filletEdges)) return;
            DA.GetData(6, ref tieRodRadius);

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
            int nrPts = 10;
            double ptDist = (double)1 / nrPts;

            for (double i = ptDist / 2; i < 1; i += ptDist)
                for (double j = ptDist / 2; j < 1; j += ptDist)
                {
                    Point3d pt = segmentsUnionBox.PointAt(i, j, 1);

                    // only project points inside bayGeometry
                    PointContainment ptIn = floorCrv.Contains(pt, Plane.WorldXY, tol);
                    if (ptIn == PointContainment.Inside)
                    {

                        Point3d projPt = Intersection.ProjectPointsToBreps(
                          new List<Brep> { shell.ToBrep() },
                          new List<Point3d> { pt },
                          new Vector3d(0, 0, -1),
                          tol)[0];

                        Line axis = new Line(projPt, pt);

                        Brep spacer = Brep.CreatePipe(
                          axis.ToNurbsCurve(), thickness / 2, false, PipeCapMode.None, false, tol, tol)[0];
                        spacer.Translate(new Vector3d(0, 0, thickness / 2));
                        spacers.Add(spacer);
                    }
                }

            // ------------ make columns --------------- //
            //double columnHeight = 3;
            double columnHeadHeight = cornerRadius * 1.5;
            double columnRadius = cornerRadius / 2;
            List<Brep> columns = new List<Brep>();
            List<Brep> columnHeads = new List<Brep>();
            foreach (Point3d corner in bayGeometry)
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

            // ------- make Tierods ----------- //

            PolylineCurve tierodCurve = (PolylineCurve)bayCurve.Duplicate();
            tierodCurve.Translate(new Vector3d(0, 0, -thickness));
            Brep tierods = Brep.CreatePipe(tierodCurve, tieRodRadius, false, PipeCapMode.None, false, tol, tol)[0];



            // move everything up: thickSegments, floorBrep, spacers, columnHeads, tierods

            double freeHeight = columnHeight - shellHeight - thickness;
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
                return ACORN_shells.Properties.Resources.ACORN_24;
            }
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("09b502b0-6794-4f57-87d2-500bdf1c3fd2"); }
        }

    }
}
