using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Rhino.Geometry;
using Karamba.GHopper.Geometry;
using Karamba.GHopper.Models;
using Karamba.Geometry;
using System.Linq;

namespace ACORN_shells
{
    public class StressLines : GH_Component
    {
        // Karamba defaults for stress line extraction
        double A_TOL = 5 / 180 * Math.PI;
        int MAX_ITER = 500;

        public StressLines()
          : base("Stress Lines", "StressLines",
              "Generate stress lines and cable profiles from preliminary Karamba model.",
              "ACORN Shells", "  Shape")
        // adding spaces to category names as per https://www.grasshopper3d.com/forum/topics/change-order-of-plugin-sub-category-c 
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Model", "M", "Analysed Karamba model.", GH_ParamAccess.item);
            pManager.AddBrepParameter("Shell", "S", "Shell Brep.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Keystone Width", "KW", "Width of keystone.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Cornerstone Width", "CW", "Width of cornerstone.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Length Param1", "L1", "Distance between stress lines 1 (circular).", GH_ParamAccess.item);
            pManager.AddNumberParameter("Length Param2", "L2", "Distance between stress lines 2 (radial).", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddBrepParameter("Shell", "S", "Shell Brep.", GH_ParamAccess.item);
            pManager.AddCurveParameter("Stress Lines 1", "SL1", "Stress lines in first principal direction (circular).", GH_ParamAccess.list);
            pManager.AddCurveParameter("Stress Lines 2", "SL2", "Stress lines in second principal direction (radial).", GH_ParamAccess.list);
            // cable curves not being used
            //pManager.AddCurveParameter("CableCurves1", "CP1", "Cable curves in first direction.", GH_ParamAccess.list);
            //pManager.AddCurveParameter("CableCurves", "CP2", "Cable curves in second direction.", GH_ParamAccess.list);
            //TESTING
            pManager.AddCurveParameter("Geodesics", "G", "Geodesic curves.", GH_ParamAccess.list);
            pManager.AddPointParameter("Source points", "SP", "Source points.", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            GH_Model ghModel = null;
            Brep shell = null;
            double keystoneWidth = 0;
            double cornerstoneWidth = 0;
            double lengthParam1 = 0;
            double lengthParam2 = 0;

            if (!DA.GetData(0, ref ghModel)) return;
            if (!DA.GetData(1, ref shell)) return;
            if (!DA.GetData(2, ref keystoneWidth)) return;
            if (!DA.GetData(3, ref cornerstoneWidth)) return;
            if (!DA.GetData(4, ref lengthParam1)) return;
            if (!DA.GetData(5, ref lengthParam2)) return;

            var model = ghModel.Value;
            var fileTol = Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance;
            var shellSurf = shell.Surfaces[0];

            // --- Find shell apex (in cartesian XYZ space and surface UV space)
            AreaMassProperties areaMassProp = AreaMassProperties.Compute(shell);
            Point3d apex = areaMassProp.Centroid;
            shellSurf.ClosestPoint(apex, out double u, out double v);
            Point2d apexUV = new Point2d(u, v);


            // --- make geodesic lines for locating source points to analyse stress lines at

            // extract shell corners and edges
            SHELLScommon.GetShellEdges(shell, out List<Curve> corners, out List<Curve> edges);

            // make geodesic lines, using shortest path from shell apex to midpoints of edges and corners
            // trim geodesics to accommodate keystone and cornerstones

            List<Curve> edgeGeodesics = new List<Curve>();
            List<Curve> cornerGeodesics = new List<Curve>();
            List<Curve> edgeGeodesicsUntrimmed = new List<Curve>(); // to add to circular stress lines set
 
            //foreach (var p in edgeMidpoints)
            foreach (Curve e in edges)
            {
                Point3d p = e.PointAtNormalizedLength(0.5);
                shellSurf.ClosestPoint(p, out u, out v);
                Point2d pUV = new Point2d(u, v);
                Curve edgeGeodesic = shellSurf.ShortPath(apexUV, pUV, fileTol);
                edgeGeodesicsUntrimmed.Add(edgeGeodesic);
                // trim for keystone
                edgeGeodesic = edgeGeodesic.Trim(CurveEnd.Start, keystoneWidth / 2 * Math.Sqrt(2)); //reduce by keystone half-diagonal (ARC?)
                edgeGeodesics.Add(edgeGeodesic); // trimmed geodesics used for generating stress surves 2
            }

            //foreach (var p in cornerMidpoints)
            foreach (Curve c in corners)
            {
                Point3d p = c.PointAtNormalizedLength(0.5);
                // get full geodesic
                shellSurf.ClosestPoint(p, out u, out v);
                Point2d pUV = new Point2d(u, v);
                Curve cornerGeodesic = shellSurf.ShortPath(apexUV, pUV, fileTol);
                // trim for keystone and cornerstone
                cornerGeodesic = cornerGeodesic.Trim(CurveEnd.End, cornerstoneWidth); // reduce by cornerstone width (ARC?) 
                cornerGeodesic = cornerGeodesic.Trim(CurveEnd.Start, keystoneWidth / 2); // reduce by keystone half-width (ARC?)
                cornerGeodesics.Add(cornerGeodesic);
            }


            // --- Find source points
            // segment for segmentation layouts, cable for post-tensioning cable sheaves (not being used, but left here just in case)
            // cable curves in between segment curves (and edges)
            var segmentSources1 = new List<Point3d>();
            var cableSources1 = new List<Point3d>();
            foreach(var l in cornerGeodesics)
            {
                var length = l.GetLength();
                int divs = (int)Math.Ceiling(length / lengthParam1);// Want ceil to use as upper bound
                var segmentParams = Enumerable.Range(0, divs + 1).Select(x => x / (double)divs).ToList();
                segmentParams.RemoveAt(0); // remove first source point, on keystone
                var cableParams = Enumerable.Range(0, divs).Select(x => (x + 0.5) / (double)divs).ToList();
                segmentSources1.AddRange(segmentParams.Select(t => l.PointAtNormalizedLength(t)));
                cableSources1.AddRange(cableParams.Select(t => l.PointAtNormalizedLength(t)));
            }

            var segmentSources2 = new List<Point3d>();
            var cableSources2 = new List<Point3d>();
            foreach (var l in edgeGeodesics)
            {
                var length = l.GetLength();
                int divs = (int)Math.Ceiling(length / lengthParam2);// Want ceil to use as upper bound
                var segmentParams = Enumerable.Range(0, divs + 1).Select(x => x / (double)divs).ToList();                
                segmentParams.RemoveAt(divs); // remove last source point, on edge - replaced by edge itself
                var cableParams = Enumerable.Range(0, divs).Select(x => (x + 0.5) / (double)divs).ToList();
                segmentSources2.AddRange(segmentParams.Select(t => l.PointAtNormalizedLength(t)));
                cableSources2.AddRange(cableParams.Select(t => l.PointAtNormalizedLength(t)));
            }


            // --- Get stress lines (uses Karamba API)
            // Circular stress curves 1 (around supports) contain points in curve from edge midpoints to centroid
            // Radial stress curves 2 (towards supports) contain points in curve from corner midpoint to centroid

            var segmentCurves1 = PrincipalStressLines(model, segmentSources1).Item1;
            var segmentCurves2 = PrincipalStressLines(model, segmentSources2).Item2;
            var cableCurves1 = PrincipalStressLines(model, cableSources1).Item1;
            var cableCurve2 = PrincipalStressLines(model, cableSources2).Item2;

            // Add apex-edge geodesic to circular stress lines set
            segmentCurves1.AddRange(edgeGeodesicsUntrimmed);

            // Add edges to radial stress lines set
            segmentCurves2.AddRange(edges);


            DA.SetData(0, shell);
            DA.SetDataList(1, segmentCurves1);
            DA.SetDataList(2, segmentCurves2);
            // cable curves not being used
            //DA.SetDataList(2, cableCurves1);
            //DA.SetDataList(3, cableCurve2);

            //TESTING
            var geodesics = new List<Curve>(cornerGeodesics);
            geodesics.AddRange(edgeGeodesics);
            var sources = new List<Point3d>(segmentSources1);
            sources.AddRange(segmentSources2);
            DA.SetDataList(3, geodesics);
            DA.SetDataList(4, sources);
        }

        private Tuple<List<Curve>, List<Curve>> PrincipalStressLines(Karamba.Models.Model model, List<Point3d> points)
        {
            var fileTol = Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance;

            var k3dPoints = points.Select(p => p.Convert()).ToList();
            var vivMesh = new Karamba.GHopper.Geometry.VivinityMesh(model);
            var sourceLines = new List<Line3>();
            foreach(var p in k3dPoints)
            {
                Line3 l;
                if(vivMesh.IntersectionLine(p, out l))
                    sourceLines.Add(l);
            }
            var result = new List<List<List<List<Line3>>>>();
            Karamba.Results.PrincipalStressLines.solve(model,
                0, sourceLines, fileTol, A_TOL, MAX_ITER, model.superimpFacsStates, out result);

            // Unpack Karamba line results and create polylines from it
            var stressLines1 = new List<Curve>();
            foreach (var i in result[0])
            {
                var polySegments = new List<Curve>();
                foreach(var j in i)
                {
                    var lines = j.Select(l => l.Convert()).ToList();
                    var polyPoints = lines.Select(l => l.From).ToList();
                    polyPoints.Add(lines.Last().To);
                    polySegments.Add(new PolylineCurve(polyPoints));
                }
                var stressLine = Curve.JoinCurves(polySegments);
                stressLines1.AddRange(stressLine);
            }

            var stressLines2 = new List<Curve>();
            foreach (var i in result[1])
            {
                var polySegments = new List<Curve>();
                foreach (var j in i)
                {
                    var lines = j.Select(l => l.Convert()).ToList();
                    var polyPoints = lines.Select(l => l.From).ToList();
                    polyPoints.Add(lines.Last().To);
                    polySegments.Add(new PolylineCurve(polyPoints));
                }
                var stressLine = Curve.JoinCurves(polySegments);
                stressLines2.AddRange(stressLine);
            }

            return Tuple.Create(stressLines1, stressLines2);
        }

        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                return ACORN_shells.Properties.Resources.stresslines;
            }
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("8d387c45-1b18-4cc3-9596-c9c9a05a470c"); }
        }


        public override GH_Exposure Exposure
        {
            get { return GH_Exposure.secondary; }
        }

    }
}